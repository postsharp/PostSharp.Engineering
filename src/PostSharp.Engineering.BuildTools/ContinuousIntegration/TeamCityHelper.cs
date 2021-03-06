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
    public static readonly string TeamcityApiBuildQueueUri = "https://tc.postsharp.net/app/rest/buildQueue";
    public static readonly string TeamCityApiRunningBuildsUri = "https://tc.postsharp.net/app/rest/builds?locator=state:running";
    public static readonly string TeamCityApiFinishedBuildsUri = "https://tc.postsharp.net/app/rest/builds?locator=state:finished";

    public static bool IsTeamCityBuild( BaseBuildSettings settings )
        => settings.ContinuousIntegration || Environment.GetEnvironmentVariable( "TEAMCITY_GIT_PATH" ) != null;

    public static bool TryGetTeamCitySourceWriteToken( out string environmentVariableName, [NotNullWhen( true )] out string? teamCitySourceWriteToken )
    {
        environmentVariableName = "TEAMCITY_SOURCE_WRITE_TOKEN";
        teamCitySourceWriteToken = Environment.GetEnvironmentVariable( environmentVariableName );

        if ( teamCitySourceWriteToken == null )
        {
            return false;
        }

        return true;
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
                "config user.email teamcity@postsharp.net",
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

        var token = Environment.GetEnvironmentVariable( "TEAMCITY_TOKEN" );

        if ( string.IsNullOrEmpty( token ) )
        {
            context.Console.WriteError( "The TEAMCITY_TOKEN environment variable is not defined." );

            return false;
        }

        TeamCityClient tc = new( token );

        var scheduledBuildId = tc.ScheduleBuild( buildTypeId );

        if ( string.IsNullOrEmpty( scheduledBuildId ) )
        {
            context.Console.WriteError( $"Failed to schedule build '{buildTypeId}'." );

            return false;
        }

        context.Console.WriteMessage( $"Scheduling build '{buildTypeId}' with build ID '{scheduledBuildId}'." );

        var scheduledBuildNumber = string.Empty;

        if ( tc.IsBuildQueued( scheduledBuildId ) )
        {
            context.Console.WriteMessage( "Waiting for build to start..." );
        }

        while ( tc.IsBuildRunning( scheduledBuildId ) || tc.IsBuildQueued( scheduledBuildId ) )
        {
            if ( tc.IsBuildRunning( scheduledBuildId ) )
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

        if ( !tc.HasBuildFinishedSuccessfully( scheduledBuildId ) )
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
            Dependencies.Model.Dependencies.All.FirstOrDefault( d => d.Name.Equals( settings.ProductName, StringComparison.OrdinalIgnoreCase ) )
            ?? TestDependencies.All.FirstOrDefault( d => d.Name.Equals( settings.ProductName, StringComparison.OrdinalIgnoreCase ) );

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
                buildTypeId = dependencyDefinition.CiBuildTypes[settings.BuildConfiguration];

                break;

            case TeamCityBuildType.Deploy:
                buildTypeId = dependencyDefinition.DeploymentBuildType;

                break;

            case TeamCityBuildType.Bump:
                buildTypeId = dependencyDefinition.BumpBuildType;

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
}