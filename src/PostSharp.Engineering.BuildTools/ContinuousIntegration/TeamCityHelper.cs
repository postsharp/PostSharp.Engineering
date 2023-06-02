// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public static class TeamCityHelper
{
    public const string TeamCityOnPremUrl = "https://tc.teamcity.com";
    public const string TeamCityOnPremTokenEnvironmentVariableName = "TEAMCITY_TOKEN";
    public const string TeamCityCloudUrl = "https://postsharp.teamcity.com";
    public const string TeamCityCloudTokenEnvironmentVariableName = "TEAMCITY_CLOUD_TOKEN";
    public const string TeamCityUsername = "teamcity@postsharp.net";
    public const string TeamcityApiBuildQueuePath = $"/app/rest/buildQueue";
    public const string TeamCityApiRunningBuildsPath = "/app/rest/builds?locator=state:running";
    public const string TeamCityApiFinishedBuildsPath = "/app/rest/builds?locator=state:finished";

    public static bool IsTeamCityBuild( CommonCommandSettings? settings = null )
        => settings?.ContinuousIntegration == true || Environment.GetEnvironmentVariable( "IS_TEAMCITY_AGENT" )?.ToLowerInvariant() == "true";

    public static bool TryConnectTeamCity( CiProjectConfiguration configuration, ConsoleHelper console, [NotNullWhen( true )] out TeamCityClient? client )
    {
        var teamcityTokenVariable = configuration.TokenEnvironmentVariableName;

        var token = Environment.GetEnvironmentVariable( teamcityTokenVariable );

        if ( string.IsNullOrEmpty( token ) )
        {
            console.WriteError( $"The {teamcityTokenVariable} environment variable is not defined." );
            client = null;

            return false;
        }

        client = new TeamCityClient( configuration.BaseUrl, token );

        return true;
    }

    public static bool TryConnectTeamCity( BuildContext context, [NotNullWhen( true )] out TeamCityClient? client )
        => TryConnectTeamCity( context.Product.DependencyDefinition.CiConfiguration, context.Console, out client );

    public static bool TryGetTeamCitySourceWriteToken( out string environmentVariableName, [NotNullWhen( true )] out string? teamCitySourceWriteToken )
    {
        environmentVariableName = "SOURCE_CODE_WRITING_TOKEN";
        teamCitySourceWriteToken = Environment.GetEnvironmentVariable( environmentVariableName );

        if ( teamCitySourceWriteToken == null )
        {
            return false;
        }

        return true;
    }

    public static string GetTeamCitySourceReadToken()
    {
        // We use TeamCity configuration parameter value.
        const string buildParameterName = "SOURCE_CODE_READING_TOKEN";
        var teamCitySourceReadToken = Environment.GetEnvironmentVariable( buildParameterName );

        if ( string.IsNullOrWhiteSpace( teamCitySourceReadToken ) )
        {
            throw new InvalidOperationException( $"The '{buildParameterName}' environment variable is not defined." );
        }

        return teamCitySourceReadToken;
    }

    /// <summary>
    /// Attempts to set Git user credentials to TeamCity. Configurations are set only for the current repository.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static bool TrySetGitIdentityCredentials( BuildContext context )
    {
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                "config user.name TeamCity",
                context.RepoDirectory ) )
        {
            return false;
        }

        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"config user.email {TeamCityUsername}",
                context.RepoDirectory ) )
        {
            return false;
        }

        return true;
    }

    public static bool TriggerTeamCityBuild( BuildContext context, TeamCityBuildCommandSettings settings, TeamCityBuildType teamCityBuildType )
    {
        // We get build type ID of the product from its DependencyDefinition by finding the definition by product name.
        if ( !TryGetBuildTypeIdFromDependencyDefinition( context, settings, teamCityBuildType, out var buildTypeId ) )
        {
            return false;
        }

        if ( !TryConnectTeamCity( context, out var tc ) )
        {
            return false;
        }

        var scheduledBuildId = tc.ScheduleBuild( context.Console, buildTypeId, "This build was triggered by command." );

        if ( string.IsNullOrEmpty( scheduledBuildId ) )
        {
            context.Console.WriteError( $"Failed to schedule build '{buildTypeId}'." );

            return false;
        }

        context.Console.WriteMessage( $"Scheduling build '{buildTypeId}' with build ID '{scheduledBuildId}'." );

        var scheduledBuildNumber = string.Empty;

        if ( tc.IsBuildQueued( context, scheduledBuildId ) )
        {
            context.Console.WriteMessage( "Waiting for build to start..." );
        }

        // Poll the running build status until it is finished.
        while ( !tc.HasBuildFinished( context, scheduledBuildId ) )
        {
            if ( tc.IsBuildRunning( context, scheduledBuildId ) )
            {
                context.Console.WriteMessage( tc.PollRunningBuildStatus( scheduledBuildId, out var buildNumber ) );
                scheduledBuildNumber = buildNumber;

                // TeamCity doesn't allow moving cursor up to rewrite the line as its build log is only a console output.
                if ( !IsTeamCityBuild( settings ) )
                {
                    context.Console.Out.Cursor.MoveUp();
                }
            }

            Thread.Sleep( 5000 );
        }

        if ( !tc.HasBuildFinishedSuccessfully( context, scheduledBuildId ) )
        {
            context.Console.WriteError( $"Build #{scheduledBuildNumber} of '{buildTypeId}' failed." );

            return false;
        }

        context.Console.WriteMessage( $"Build #{scheduledBuildNumber} in progress: 100%" );
        context.Console.WriteSuccess( $"Build #{scheduledBuildNumber} of '{buildTypeId}' has finished successfully." );

        return true;
    }

    private static bool TryGetBuildTypeIdFromDependencyDefinition(
        BuildContext context,
        TeamCityBuildCommandSettings settings,
        TeamCityBuildType teamCityBuildType,
        [NotNullWhen( true )] out string? buildTypeId )
    {
        // Product name must be specified.
        if ( string.IsNullOrEmpty( settings.ProductName ) )
        {
            context.Console.WriteError( $"No product specified for {teamCityBuildType}." );
            buildTypeId = null;

            return false;
        }

        // DependencyDefinition is found by its product name.
        var dependencyDefinition =
            context.Product.ProductFamily.GetDependencyDefinitionOrNull( settings.ProductName );

        if ( dependencyDefinition == null )
        {
            context.Console.WriteError( $"Dependency definition for '{settings.ProductName}' doesn't exist." );
            buildTypeId = null;

            return false;
        }

        // We get the required build type ID of the product from the DependencyDefinition.
        switch ( teamCityBuildType )
        {
            case TeamCityBuildType.Build:
                buildTypeId = dependencyDefinition.CiConfiguration.BuildTypes[settings.BuildConfiguration];

                break;

            case TeamCityBuildType.Deploy:
                buildTypeId = dependencyDefinition.CiConfiguration.DeploymentBuildType;

                break;

            case TeamCityBuildType.Bump:
                buildTypeId = dependencyDefinition.CiConfiguration.VersionBumpBuildType;

                break;

            default:
                buildTypeId = null;

                return false;
        }

        if ( buildTypeId == null )
        {
            context.Console.WriteError( $"'{settings.ProductName}' has no known build type ID for build type '{teamCityBuildType}'." );

            return false;
        }

        return true;
    }
    
    public static CiProjectConfiguration CreateConfiguration(
        string teamCityProjectId,
        string buildAgentType,
        bool hasVersionBump = true,
        BuildConfiguration debugBuildDependency = BuildConfiguration.Debug,
        BuildConfiguration releaseBuildDependency = BuildConfiguration.Release,
        BuildConfiguration publicBuildDependency = BuildConfiguration.Public,
        bool isCloudInstance = true )
    {
        var buildTypes = new ConfigurationSpecific<string>(
            $"{teamCityProjectId}_{debugBuildDependency}Build",
            $"{teamCityProjectId}_{releaseBuildDependency}Build",
            $"{teamCityProjectId}_{publicBuildDependency}Build" );

        string? versionBumpBuildType = null;

        if ( hasVersionBump )
        {
            versionBumpBuildType = $"{teamCityProjectId}_VersionBump";
        }

        var deploymentBuildType = $"{teamCityProjectId}_PublicDeployment";
        string tokenEnvironmentVariableName;
        string baseUrl;

        if ( isCloudInstance )
        {
            tokenEnvironmentVariableName = TeamCityCloudTokenEnvironmentVariableName;
            baseUrl = TeamCityCloudUrl;
        }
        else
        {
            tokenEnvironmentVariableName = TeamCityOnPremTokenEnvironmentVariableName;
            baseUrl = TeamCityOnPremUrl;
        }

        return new CiProjectConfiguration( teamCityProjectId, buildTypes, deploymentBuildType, versionBumpBuildType, tokenEnvironmentVariableName, baseUrl, buildAgentType );
    }

    private static string ReplaceDots( string input, string substitute ) => input.Replace( ".", substitute, StringComparison.Ordinal );

    public static string GetProjectIdWithParentProjectId( string projectName, string parentProjectId )
    {
        var subProjectId = ReplaceDots( projectName, "" ).Replace( " ", "", StringComparison.Ordinal ); 
        
        var projectId = $"{parentProjectId}_{subProjectId}";

        return projectId;
    }
    
    public static string GetProjectId( string projectName, string? parentProjectName = null, string? productFamilyVersion = null )
    {
        string parentProjectId;
        
        if ( parentProjectName == null )
        {
            parentProjectId = "";
        }
        else
        {
            parentProjectId = ReplaceDots( parentProjectName, "" );

            if ( productFamilyVersion != null )
            {
                parentProjectId = $"{parentProjectId}_{parentProjectId}{ReplaceDots( productFamilyVersion, "" )}";
            }
        }

        var projectId = GetProjectIdWithParentProjectId( projectName, parentProjectId );

        return projectId;
    }
}