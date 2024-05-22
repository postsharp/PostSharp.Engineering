// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.VisualStudio.Services.Common;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Build.Triggers;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model.BuildSteps;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public static class TeamCityHelper
{
    public const string TeamCityOnPremUrl = "https://tc.postsharp.net";
    public const string TeamCityOnPremTokenEnvironmentVariableName = "TEAMCITY_TOKEN";
    public const string TeamCityCloudUrl = "https://postsharp.teamcity.com";
    public const string TeamCityCloudTokenEnvironmentVariableName = "TEAMCITY_CLOUD_TOKEN";
    public const string TeamCityUsername = "teamcity@postsharp.net";
    public const string TeamcityApiBuildQueuePath = $"/app/rest/buildQueue";
    public const string TeamCityApiRunningBuildsPath = "/app/rest/builds?locator=state:running";
    public const string TeamCityApiFinishedBuildsPath = "/app/rest/builds?locator=state:finished";

    public static bool IsTeamCityBuild( CommonCommandSettings settings )
        => settings.SimulateContinuousIntegration || Environment.GetEnvironmentVariable( "IS_TEAMCITY_AGENT" )?.ToLowerInvariant() == "true";

    public static ImmutableDictionary<string, string?> GetSimulatedContinuousIntegrationEnvironmentVariables( CommonCommandSettings settings )
    {
        if ( settings.SimulateContinuousIntegration )
        {
            var isIsTeamCityAgentEnvironmentVariableSet = Environment.GetEnvironmentVariable( "IS_TEAMCITY_AGENT" )?.ToLowerInvariant() == "true";

            if ( !isIsTeamCityAgentEnvironmentVariableSet )
            {
                return new Dictionary<string, string?> { { "IS_TEAMCITY_AGENT", "true" } }.ToImmutableDictionary();
            }
        }

        return ImmutableDictionary<string, string?>.Empty;
    }

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

    public static bool TriggerTeamCityBuild(
        BuildContext context,
        CommonCommandSettings settings,
        string productFamilyName,
        string productFamilyVersion,
        string productName,
        TeamCityBuildType buildType,
        BuildConfiguration? buildConfiguration )
    {
        if ( !TryGetDependencyDefinition( context, productFamilyName, productFamilyVersion, productName, out var dependencyDefinition ) )
        {
            return false;
        }

        if ( !TryGetBuildTypeId( context, dependencyDefinition, buildType, buildConfiguration, out var buildTypeId ) )
        {
            return false;
        }

        if ( !TryConnectTeamCity( dependencyDefinition.CiConfiguration, context.Console, out var tc ) )
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

        void ClearLine( int length )
        {
            // TeamCity doesn't allow moving cursor up to rewrite the line as its build log is only a console output.
            if ( !IsTeamCityBuild( settings ) )
            {
                context.Console.Out.Cursor.MoveUp();
                context.Console.Out.WriteLine( new string( ' ', length ) );
                context.Console.Out.Cursor.MoveUp();
            }
        }

        if ( tc.IsBuildQueued( context.Console, scheduledBuildId ) )
        {
            var message = "Waiting for build to start...";
            context.Console.WriteMessage( message );

            while ( tc.IsBuildQueued( context.Console, scheduledBuildId ) )
            {
                Thread.Sleep( 5000 );
            }

            ClearLine( message.Length );
        }

        // Poll the running build status until it is finished.
        while ( !tc.HasBuildFinished( context.Console, scheduledBuildId ) )
        {
            var message = tc.PollRunningBuildStatus( scheduledBuildId, out var buildNumber );
            context.Console.WriteMessage( message );
            scheduledBuildNumber = buildNumber;

            Thread.Sleep( 5000 );

            ClearLine( message.Length );
        }

        if ( !tc.HasBuildFinishedSuccessfully( context.Console, scheduledBuildId ) )
        {
            context.Console.WriteError( $"Build #{scheduledBuildNumber} of '{buildTypeId}' failed." );

            return false;
        }

        context.Console.WriteSuccess( $"Build #{scheduledBuildNumber} of '{buildTypeId}' has finished successfully." );

        return true;
    }

    private static bool TryGetDependencyDefinition(
        BuildContext context,
        string productFamilyName,
        string productFamilyVersion,
        string productName,
        [NotNullWhen( true )] out DependencyDefinition? dependencyDefinition )
    {
        dependencyDefinition = null;

        if ( !ProductFamily.TryGetFamily( productFamilyName, productFamilyVersion, out var productFamily ) )
        {
            context.Console.WriteError( $"Unknown product family '{productFamilyName}' version '{productFamilyVersion}'." );

            return false;
        }

        if ( !productFamily.TryGetDependencyDefinition( productName, out dependencyDefinition ) )
        {
            context.Console.WriteError(
                $"Dependency definition '{productName}' not found in '{productFamily.Name}' product family version '{productFamily.Version}'." );

            return false;
        }

        return true;
    }

    private static bool TryGetBuildTypeId(
        BuildContext context,
        DependencyDefinition dependencyDefinition,
        TeamCityBuildType buildType,
        BuildConfiguration? buildConfiguration,
        [NotNullWhen( true )] out string? ciBuildTypeId )
    {
        ciBuildTypeId = null;

        // We get the required build type ID of the product from the DependencyDefinition.
        switch ( buildType )
        {
            case TeamCityBuildType.Build:
                if ( buildConfiguration == null )
                {
                    context.Console.WriteError( "It is required to specify the build configuration for building a project." );

                    return false;
                }

                ciBuildTypeId = dependencyDefinition.CiConfiguration.BuildTypes[buildConfiguration.Value];

                break;

            case TeamCityBuildType.Deploy:
                ciBuildTypeId = dependencyDefinition.CiConfiguration.DeploymentBuildType;

                break;

            case TeamCityBuildType.Bump:
                ciBuildTypeId = dependencyDefinition.CiConfiguration.VersionBumpBuildType;

                break;

            default:
                ciBuildTypeId = null;

                break;
        }

        if ( ciBuildTypeId == null )
        {
            context.Console.WriteError( $"'{dependencyDefinition.Name}' product has no known build type ID for build type '{buildType}'." );

            return false;
        }

        return true;
    }

    public static CiProjectConfiguration CreateConfiguration(
        TeamCityProjectId teamCityProjectId,
        bool hasVersionBump = true,
        bool isCloudInstance = true,
        bool pullRequestRequiresStatusCheck = true,
        string? pullRequestStatusCheckBuildType = null )
    {
        var buildTypes = new ConfigurationSpecific<string>(
            $"{teamCityProjectId}_DebugBuild",
            $"{teamCityProjectId}_ReleaseBuild",
            $"{teamCityProjectId}_PublicBuild" );

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
            pullRequestRequiresStatusCheck,
            pullRequestStatusCheckBuildType );
    }

    private static string ReplaceDots( string input, string substitute = "" ) => input.Replace( ".", substitute, StringComparison.Ordinal );

    private static string ReplaceSpaces( string input, string substitute = "" ) => input.Replace( " ", substitute, StringComparison.Ordinal );

    private static string GetProjectIdFromName( string projectName ) => ReplaceDots( ReplaceSpaces( projectName ) );

    public static TeamCityProjectId GetProjectIdWithParentProjectId( string projectName, string? parentProjectId )
    {
        var subProjectId = GetProjectIdFromName( projectName );

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
            parentProjectId = GetProjectIdFromName( parentProjectName );

            if ( productFamilyVersion != null )
            {
                parentProjectId = $"{parentProjectId}_{parentProjectId}{ReplaceDots( productFamilyVersion )}";
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
        context.Console.WriteMessage( $"Creating project '{name}', ID '{id}', parent project ID '{parentId}'." );

        if ( !tc.TryCreateProject( context.Console, name, id, parentId ) )
        {
            return false;
        }

        if ( vcsRootId != null )
        {
            context.Console.WriteMessage( $"Setting versioned settings for project '{name}' ('{id}') using VCS root ID '{vcsRootId}'." );

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

    private static bool TryCreateVcsRoot( TeamCityClient tc, BuildContext context, string? projectId, [NotNullWhen( true )] out string? vcsRootId )
    {
        projectId ??= "_Root";

        context.Console.WriteMessage( $"Retrieving VCS roots of '{projectId}' project." );

        if ( !tc.TryGetVcsRoots( context.Console, projectId, out var vcsRoots ) )
        {
            vcsRootId = null;

            return false;
        }

        // The URLs and IDs can differ, so we search by name.
        // Eg. https://postsharp@dev.azure.com/postsharp/Engineering/_git/PostSharp.Engineering.Test.TestProduct
        // and "https://dev.azure.com/postsharp/Engineering/_git/PostSharp.Engineering.Test.TestProduct
        // are both valid and refer to the same project.
        var vcsRootIdsByName = vcsRoots.Value.ToDictionary(
            root => VcsUrlParser.TryGetRepository( root.Url, out var repository )
                ? repository.Name
                : throw new InvalidOperationException( $"Unknown VCS provider of '{root.Url}' repository." ),
            r => r.Id );

        var repository = context.Product.DependencyDefinition.VcsRepository;

        if ( vcsRootIdsByName.TryGetValue( repository.Name, out vcsRootId ) )
        {
            context.Console.WriteMessage( $"Using existing '{vcsRootId}' VCS root" );
        }
        else
        {
            var familyVersion = context.Product.ProductFamily.Version;
            var url = repository.TeamCityRemoteUrl;
            var defaultBranch = $"refs/heads/{context.Product.DependencyDefinition.Branch}";

            var branchSpecification = new List<string>
            {
                $"+:refs/heads/(topic/{familyVersion}/*)",
                $"+:refs/heads/(feature/{familyVersion}/*)",
                $"+:refs/heads/(experimental/{familyVersion}/*)",
                $"+:refs/heads/(merge/{familyVersion}/*)",
                $"+:refs/heads/({context.Product.DependencyDefinition.Branch})"
            };

            if ( context.Product.DependencyDefinition.ReleaseBranch != null )
            {
                branchSpecification.Add( $"+:refs/heads/({context.Product.DependencyDefinition.ReleaseBranch})" );
            }

            context.Console.WriteMessage( $"Creating '{url}' VCS root in '{projectId}' project for '{familyVersion}' family version." );

            if ( !tc.TryCreateVcsRoot( context.Console, url, projectId, defaultBranch, branchSpecification, out var vcsRootName, out vcsRootId ) )
            {
                return false;
            }

            context.Console.WriteMessage( $"Created '{vcsRootName}' VCS root ID '{vcsRootId}'." );
        }

        return true;
    }

    public static bool TryCreateVcsRoot( BuildContext context, string? projectId )
    {
        if ( !TryConnectTeamCity( context, out var tc ) )
        {
            return false;
        }

        return TryCreateVcsRoot( tc, context, projectId, out _ );
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

        if ( !TryCreateVcsRoot( tc, context, parentProjectId, out var vcsRootId ) )
        {
            return false;
        }

        if ( !TryCreateProject( tc, context, projectName, projectId, parentProjectId, vcsRootId ) )
        {
            return false;
        }

        return true;
    }

    internal static void GenerateTeamCityConfiguration( BuildContext context, TeamCityProject project )
    {
        var content = new StringWriter();
        project.GenerateTeamcityCode( content );

        var filePath = Path.Combine( context.RepoDirectory, ".teamcity", "settings.kts" );

        if ( !File.Exists( filePath ) || File.ReadAllText( filePath ) != content.ToString() )
        {
            context.Console.WriteWarning( $"Replacing '{filePath}'." );
            File.WriteAllText( filePath, content.ToString() );
        }

        context.Console.WriteSuccess( "Continuous integration scripts generated." );
    }

    internal static bool TryGenerateConsolidatedTeamcityConfiguration( BuildContext context )
    {
        // This method is implemented so it preserves the order of all entities in the resulting script.

        if ( !TryConnectTeamCity( context, out var tc ) )
        {
            return false;
        }

        var tcConfigurations = new List<TeamCityBuildConfiguration>();

        if ( !tc.TryGetOrderedSubprojectsRecursively(
                context.Console,
                context.Product.DependencyDefinition.CiConfiguration.ProjectId.ParentId,
                out var subprojects ) )
        {
            return false;
        }

        var consolidatedProjectName = context.Product.ProductName;

        var buildConfigurations = new List<(string ProjectId, string ProjectName, string BuildConfigurationId, HashSet<string> SnapshotDependencies)>();

        var buildConfigurationsById =
            new Dictionary<string, (string ProjectId, string ProjectName, string BuildConfigurationId, HashSet<string> SnapshotDependencies)>();

        var buildConfigurationsByKind =
            new Dictionary<string, List<(string ProjectId, string ProjectName, string BuildConfigurationId, HashSet<string> SnapshotDependencies)>>();

        // Exclude the consolidated build project.
        subprojects = subprojects.Where( p => !p.Id.EndsWith( $"_{consolidatedProjectName}", StringComparison.Ordinal ) ).ToImmutableArray();

        foreach ( var project in subprojects )
        {
            if ( !tc.TryGetProjectsBuildConfigurations( context.Console, project.Id, out var projectsBuildConfigurations ) )
            {
                return false;
            }

            foreach ( var buildConfigurationId in projectsBuildConfigurations )
            {
                if ( !tc.TryGetBuildConfigurationsSnapshotDependencies( context.Console, buildConfigurationId, out var snapshotDependencies ) )
                {
                    return false;
                }

                var buildConfigurationKind = buildConfigurationId.Split( '_' ).Last();

                if ( !buildConfigurationsByKind.TryGetValue( buildConfigurationKind, out var buildConfigurationsOfKind ) )
                {
                    buildConfigurationsOfKind =
                        new List<(string ProjectId, string ProjectName, string BuildConfigurationId, HashSet<string> SnapshotDependencies)>();

                    buildConfigurationsByKind.Add( buildConfigurationKind, buildConfigurationsOfKind );
                }

                var buildConfiguration = (project.Id, project.Name, buildConfigurationId, snapshotDependencies.Value.ToHashSet());
                buildConfigurations.Add( buildConfiguration );
                buildConfigurationsOfKind.Add( buildConfiguration );
                buildConfigurationsById.Add( buildConfigurationId, buildConfiguration );
            }
        }

        // Debug Build
        const string debugBuildObjectName = "DebugBuild";

        var consolidatedDebugBuildSnapshotDependencies =
            buildConfigurationsByKind[debugBuildObjectName].Select( c => new TeamCitySnapshotDependency( c.BuildConfigurationId, true ) );

        tcConfigurations.Add(
            new TeamCityBuildConfiguration( debugBuildObjectName, "Build [Debug]" )
            {
                SnapshotDependencies = consolidatedDebugBuildSnapshotDependencies.ToArray()
            } );

        // Downstream Merge

        // Downstream merge of the consolidated build repo itself needs to be done manually,
        // because the TeamCity script needs to be regenerated in each product family version. 

        const string downstreamMergeObjectName = "DownstreamMerge";

        if ( buildConfigurationsByKind.TryGetValue( downstreamMergeObjectName, out var downstreamMergeBuildConfigurations ) )
        {
            var consolidatedDownstreamMergeSnapshotDependencies =
                downstreamMergeBuildConfigurations.Select( c => new TeamCitySnapshotDependency( c.BuildConfigurationId, true ) );

            var consolidatedDownstreamMergeBuildTriggers = new IBuildTrigger[] { new NightlyBuildTrigger( 23, true ) };

            tcConfigurations.Add(
                new TeamCityBuildConfiguration( downstreamMergeObjectName, "Merge Downstream" )
                {
                    SnapshotDependencies = consolidatedDownstreamMergeSnapshotDependencies.ToArray(),
                    BuildTriggers = consolidatedDownstreamMergeBuildTriggers
                } );
        }

        // Release Build
        const string releaseBuildObjectName = "ReleaseBuild";

        var consolidatedReleaseBuildSnapshotDependencies =
            buildConfigurationsByKind[releaseBuildObjectName].Select( c => new TeamCitySnapshotDependency( c.BuildConfigurationId, true ) );

        tcConfigurations.Add(
            new TeamCityBuildConfiguration( releaseBuildObjectName, "Build [Release]" )
            {
                SnapshotDependencies = consolidatedReleaseBuildSnapshotDependencies.ToArray()
            } );

        // Version bump and public build
        const string publicBuildObjectName = "PublicBuild";

        var consolidatedPublicBuildSnapshotDependencies = new List<TeamCitySnapshotDependency>();
        var projectDependenciesByProjectId = new Dictionary<string, HashSet<string>>();

        foreach ( var publicBuildConfiguration in buildConfigurationsByKind[publicBuildObjectName] )
        {
            consolidatedPublicBuildSnapshotDependencies.Add( new TeamCitySnapshotDependency( publicBuildConfiguration.BuildConfigurationId, true ) );

            if ( !projectDependenciesByProjectId.TryGetValue( publicBuildConfiguration.BuildConfigurationId, out var projectDependencies ) )
            {
                projectDependencies = new HashSet<string>();
                projectDependenciesByProjectId.Add( publicBuildConfiguration.ProjectId, projectDependencies );
            }

            // We check for presence, because some dependencies can come from other project families.
            // E.g. PostSharp for Metalama.Vsx.
            projectDependencies.AddRange(
                publicBuildConfiguration.SnapshotDependencies.Select( d => buildConfigurationsById.TryGetValue( d, out var c ) ? c.ProjectId : null )
                    .Where( c => c != null )
                    .Select( c => c! ) );
        }

        const string versionBumpObjectName = "VersionBump";

        var consolidatedVersionBumpSteps = new List<TeamCityBuildStep>();
        var bumpedProjects = new HashSet<string>();

        var familyName = context.Product.ProductFamily.Name;
        var familyVersion = context.Product.ProductFamily.Version;

        var success = true;

        foreach ( var versionBumpBuildConfiguration in buildConfigurationsByKind[versionBumpObjectName] )
        {
            var bumpedProjectId = versionBumpBuildConfiguration.ProjectId;
            var bumpedProjectName = versionBumpBuildConfiguration.ProjectName;

            foreach ( var projectDependencyId in projectDependenciesByProjectId[bumpedProjectId] )
            {
                if ( !bumpedProjects.Contains( projectDependencyId ) )
                {
                    context.Console.WriteError( $"Incorrect projects order. '{bumpedProjectId}' depends on '{projectDependencyId}', but is ordered earlier." );
                    success = false;
                }
            }

            consolidatedVersionBumpSteps.Add(
                new TeamCityEngineeringCommandBuildStep(
                    $"Bump{versionBumpBuildConfiguration.ProjectId.Split( '_' ).Last()}",
                    $"Trigger version bump of {bumpedProjectName}",
                    "teamcity run bump",
                    $"{familyName} {familyVersion} {bumpedProjectName}" ) );

            bumpedProjects.Add( bumpedProjectId );
        }

        if ( !success )
        {
            return false;
        }

        // Consolidated version bumps don't run for all versions at the same time to avoid build agent starvation.
        var consolidatedVersionBumpBuildTriggerMinute = 0;

        if ( context.Product.ProductFamily.UpstreamProductFamily != null )
        {
            var previousProductFamily = context.Product.ProductFamily.UpstreamProductFamily;

            while ( previousProductFamily != null )
            {
                consolidatedVersionBumpBuildTriggerMinute += 10;
                previousProductFamily = previousProductFamily.UpstreamProductFamily;
            }
        }

        var consolidatedVersionBumpBuildTriggers = new IBuildTrigger[] { new NightlyBuildTrigger( 1, consolidatedVersionBumpBuildTriggerMinute, false ) };

        tcConfigurations.Add(
            new TeamCityBuildConfiguration(
                versionBumpObjectName,
                "1. Version Bump",
                context.Product.BuildAgentRequirements )
            {
                BuildSteps = consolidatedVersionBumpSteps.ToArray(), BuildTriggers = consolidatedVersionBumpBuildTriggers
            } );

        var consolidatedPublicBuildBuildTriggers = new IBuildTrigger[] { new NightlyBuildTrigger( 2, true ) };

        tcConfigurations.Add(
            new TeamCityBuildConfiguration( publicBuildObjectName, "2. Build [Public]" )
            {
                SnapshotDependencies = consolidatedPublicBuildSnapshotDependencies.ToArray(), BuildTriggers = consolidatedPublicBuildBuildTriggers
            } );

        // Public deployment
        const string publicDeploymentObjectName = "PublicDeployment";

        var publicDeploymentBuildConfigurations = buildConfigurationsByKind[publicDeploymentObjectName];
        var publicDeploymentBuildConfigurationIds = publicDeploymentBuildConfigurations.Select( c => c.BuildConfigurationId ).ToArray();

        // Include dependants of the public deployment build configurations, like search update.
        var publicDeploymentDependants = buildConfigurations.Where( c => c.SnapshotDependencies.Intersect( publicDeploymentBuildConfigurationIds ).Any() )
            .Select( c => c.BuildConfigurationId )
            .Except( publicDeploymentBuildConfigurationIds )
            .ToArray();

        var consolidatedPublicDeploymentSnapshotDependencies =
            publicDeploymentBuildConfigurationIds
                .Concat( publicDeploymentDependants )
                .Select( c => new TeamCitySnapshotDependency( c, true ) );

        tcConfigurations.Add(
            new TeamCityBuildConfiguration( publicDeploymentObjectName, "3. Deploy [Public]" )
            {
                SnapshotDependencies = consolidatedPublicDeploymentSnapshotDependencies.ToArray()
            } );

        var tcProject = new TeamCityProject( tcConfigurations.ToArray() );
        GenerateTeamCityConfiguration( context, tcProject );

        return true;
    }
}