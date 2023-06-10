﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
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

    public static bool IsTeamCityBuild( CommonCommandSettings settings )
        => settings.SimulateContinuousIntegration || Environment.GetEnvironmentVariable( "IS_TEAMCITY_AGENT" )?.ToLowerInvariant() == "true";

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

        if ( tc.IsBuildQueued( context.Console, scheduledBuildId ) )
        {
            context.Console.WriteMessage( "Waiting for build to start..." );
        }

        // Poll the running build status until it is finished.
        while ( !tc.HasBuildFinished( context.Console, scheduledBuildId ) )
        {
            if ( tc.IsBuildRunning( context.Console, scheduledBuildId ) )
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

        if ( !tc.HasBuildFinishedSuccessfully( context.Console, scheduledBuildId ) )
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
        TeamCityProjectId teamCityProjectId,
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

        return new CiProjectConfiguration(
            teamCityProjectId,
            buildTypes,
            deploymentBuildType,
            versionBumpBuildType,
            tokenEnvironmentVariableName,
            baseUrl,
            buildAgentType );
    }

    private static string ReplaceDots( string input, string substitute ) => input.Replace( ".", substitute, StringComparison.Ordinal );

    public static TeamCityProjectId GetProjectIdWithParentProjectId( string projectName, string? parentProjectId )
    {
        var subProjectId = ReplaceDots( projectName, "" ).Replace( " ", "", StringComparison.Ordinal );

        var projectId = parentProjectId == null ? subProjectId : $"{parentProjectId}_{subProjectId}";

        return new TeamCityProjectId( projectId, parentProjectId ?? "_Root" );
    }

    public static TeamCityProjectId GetProjectId( string projectName, string? parentProjectName = null, string? productFamilyVersion = null )
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

    public static void SendImportDataMessage( string type, string path, string flowId, bool failOnNoData )
    {
        var date = DateTimeOffset.Now;
        var timestamp = $"{date:yyyy-MM-dd'T'HH:mm:ss.fff}{date.Offset.Ticks:+;-;}{date.Offset:hhmm}";

        Console.WriteLine(
            "##teamcity[importData type='{0}' path='{1}' flowId='{2}' timestamp='{3}' whenNoDataPublished='{4}']",
            type,
            path,
            flowId,
            timestamp,
            failOnNoData ? "error" : "warning" );
    }

    private static bool TryCreateProject( TeamCityClient tc, BuildContext context, string name, string id, string? parentId = null, string? vcsRootId = null )
    {
        context.Console.WriteMessage( $"Creating project \"{name}\", ID \"{id}\", parent project ID \"{parentId}\"." );

        if ( !tc.TryCreateProject( context.Console, name, id, parentId ) )
        {
            return false;
        }

        if ( vcsRootId != null )
        {
            context.Console.WriteMessage( $"Setting versioned settings for project \"{name}\" (\"{id}\") using VCS root ID \"{vcsRootId}\"." );

            if ( !tc.TrySetProjectVersionedSettings( context.Console, id, vcsRootId ) )
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryCreateProject( BuildContext context, string name, string id, string? parentId = null, string? vcsRootId = null )
    {
        if ( !TryConnectTeamCity( context, out var tc ) )
        {
            return false;
        }

        return TryCreateProject( tc, context, name, id, parentId, vcsRootId );
    }

    public static bool TryCreateProject( BuildContext context )
    {
        if ( !TryConnectTeamCity( context, out var tc ) )
        {
            return false;
        }

        var projectId = context.Product.DependencyDefinition.CiConfiguration.ProjectId.Id;
        var parentProjectId = context.Product.DependencyDefinition.CiConfiguration.ProjectId.ParentId;
        var projectName = context.Product.DependencyDefinition.Name;

        if ( !tc.TryGetVcsRoots( context.Console, parentProjectId, out var vcsRoots ) )
        {
            return false;
        }

        // The URLs and IDs can differ, so we search by name.
        // Eg. https://postsharp@dev.azure.com/postsharp/Engineering/_git/PostSharp.Engineering.Test.TestProduct
        // and "https://dev.azure.com/postsharp/Engineering/_git/PostSharp.Engineering.Test.TestProduct
        // are both valid and refer to the same project.
        var vcsRootsIdsByName = vcsRoots.Value.ToDictionary(
            r => VcsUrlParser.TryGetName( r.Url, out var name )
                ? name
                : throw new InvalidOperationException( $"Unknown VCS provider of \"{r.Url}\" repository." ),
            r => r.Id );

        if ( !GitHelper.TryGetRemoteUrl( context, out var remoteUrl )
             || !VcsUrlParser.TryGetName( remoteUrl, out var remoteVcsName ) )
        {
            return false;
        }

        if ( vcsRootsIdsByName.TryGetValue( remoteVcsName, out var vcsRootId ) )
        {
            context.Console.WriteMessage( $"Using existing \"{vcsRootId}\" VCS root" );
        }
        else
        {
            var familyVersion = context.Product.ProductFamily.Version;
            context.Console.WriteMessage( $"Creating \"{remoteUrl}\" VCS root in \"{parentProjectId}\" project for \"{familyVersion}\" family version." );
            
            if ( !tc.TryCreateVcsRoot( context.Console, remoteUrl, parentProjectId, familyVersion, out var vcsRootName, out vcsRootId ) )
            {
                return false;
            }

            context.Console.WriteMessage( $"Created \"{vcsRootName}\" VCS root ID \"{vcsRootId}\"." );
        }

        return TryCreateProject( tc, context, projectName, projectId, parentProjectId, vcsRootId );
    }
}