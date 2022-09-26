// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    /// <summary>
    /// Fetches the artifacts of the build dependencies.
    /// </summary>
    public class FetchDependencyCommand : BaseCommand<FetchDependenciesCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, FetchDependenciesCommandSettings settings )
        {
            context.Console.WriteHeading( "Fetching build artifacts" );

            if ( !settings.TryGetBuildConfiguration( context, out var configuration ) )
            {
                return false;
            }

            if ( !DependenciesOverrideFile.TryLoad( context, configuration, out var dependenciesOverrideFile ) )
            {
                return false;
            }

            if ( !FetchDependencies( context, configuration, dependenciesOverrideFile, settings.Update, settings ) )
            {
                return false;
            }

            if ( !dependenciesOverrideFile.TrySave( context ) )
            {
                return false;
            }

            return true;
        }

        public static bool FetchDependencies(
            BuildContext context,
            BuildConfiguration configuration,
            DependenciesOverrideFile dependenciesOverrideFile,
            bool forceUpdate = false,
            FetchDependenciesCommandSettings? settings = null )
        {
            settings ??= new FetchDependenciesCommandSettings();

            if ( !forceUpdate )
            {
                forceUpdate = settings.Update;
            }

            DependencyDefinition? GetDependencyDefinition( KeyValuePair<string, DependencySource> dependency )
            {
                var dependencyDefinition = context.Product.GetDependency( dependency.Key );

                if ( dependencyDefinition == null )
                {
                    context.Console.WriteWarning( $"The dependency '{dependency.Key}' is not configured. Ignoring." );
                }

                return dependencyDefinition;
            }

            var dependencies = dependenciesOverrideFile
                .Dependencies
                .Where( d => d.Value.Origin != DependencyConfigurationOrigin.Transitive )
                .Select( d => (d.Value, GetDependencyDefinition( d )) )
                .Where( d => d.Item2 != null )
                .Select( d => new Dependency( d.Value, d.Item2! ) )
                .ToList();

            if ( dependencies.Count == 0 )
            {
                context.Console.WriteWarning( "No dependencies to fetch." );

                return true;
            }

            var token = Environment.GetEnvironmentVariable( "TEAMCITY_TOKEN" );

            if ( string.IsNullOrEmpty( token ) )
            {
                context.Console.WriteError( "The TEAMCITY_TOKEN environment variable is not defined." );

                return false;
            }

            var teamcity = new TeamCityClient( token );

            var iterationDependencies = dependencies.ToImmutableDictionary( d => d.Definition.Name, d => d );
            var dependencyDictionary = dependencies.ToImmutableDictionary( d => d.Definition.Name, d => d );

            while ( iterationDependencies.Count > 0 )
            {
                // Download artefacts that are not transitive dependencies.
                if ( !ResolveBuildNumbersFromBranches( context, configuration, teamcity, iterationDependencies, forceUpdate ) ||
                     !ResolveLocalDependencies( context, iterationDependencies ) )
                {
                    return false;
                }

                if ( !DownloadArtifacts( context, teamcity, iterationDependencies ) )
                {
                    return false;
                }

                // Find implicit transitive dependencies.
                if ( !TryGetTransitiveDependencies(
                        context,
                        dependencyDictionary,
                        iterationDependencies,
                        dependenciesOverrideFile,
                        out var newDependencies ) )
                {
                    return false;
                }

                iterationDependencies = newDependencies;
                dependencyDictionary = dependencyDictionary.AddRange( newDependencies );
            }

            context.Console.WriteSuccess( "Fetching build artifacts was successful" );

            return true;
        }

        private static bool TryGetTransitiveDependencies(
            BuildContext context,
            ImmutableDictionary<string, Dependency> allDependencies,
            ImmutableDictionary<string, Dependency> directDependencies,
            DependenciesOverrideFile dependenciesOverrideFile,
            [NotNullWhen( true )] out ImmutableDictionary<string, Dependency>? newDependencies )
        {
            var newDependenciesBuilder = ImmutableDictionary.CreateBuilder<string, Dependency>();

            newDependencies = null;

            foreach ( var directDependency in directDependencies.Values )
            {
                if ( directDependency.Source.SourceKind == DependencySourceKind.Feed )
                {
                    // The dependency is managed by NuGet.
                    // Currency, we don't support retrieving transitive dependencies from NuGet packages.
                    continue;
                }

                var versionFile = Project.FromFile( directDependency.Source.VersionFile!, new ProjectOptions() );

                var transitiveDependencies = versionFile.Items.Where( i => i.ItemType == directDependency.Definition.NameWithoutDot + "Dependencies" );

                foreach ( var transitiveDependency in transitiveDependencies )
                {
                    var name = transitiveDependency.EvaluatedInclude;

                    if ( newDependenciesBuilder.ContainsKey( name ) )
                    {
                        // This dependency is transitively included twice through different paths.
                        continue;
                    }

                    var sourceKindString = transitiveDependency.GetMetadata( "SourceKind" )?.EvaluatedValue;

                    if ( sourceKindString == null || !Enum.TryParse<DependencySourceKind>( sourceKindString, out var sourceKind ) )
                    {
                        context.Console.WriteWarning( $"Cannot parse the source kind '{sourceKindString}' in '{directDependency.Source.VersionFile}'." );

                        continue;
                    }

                    if ( allDependencies.TryGetValue( name, out _ ) )
                    {
                        continue;
                    }

                    // Get the DependencyDefinition.
                    var dependencyDefinition = Model.Dependencies.All.SingleOrDefault( d => d.Name == name )
                                               ?? TestDependencies.All.SingleOrDefault( d => d.Name == name );

                    if ( dependencyDefinition == null )
                    {
                        context.Console.WriteError(
                            $"Cannot find the dependency definition for '{name}' referenced by '{directDependency.Definition.Name}'. The dependency must be defined in PostSharp.Engineering." );

                        return false;
                    }

                    // Create a DependencySource.
                    DependencySource dependencySource;

                    switch ( sourceKind )
                    {
                        case DependencySourceKind.BuildServer:
                            var buildNumber = transitiveDependency.GetMetadataValue( "BuildNumber" );
                            var ciBuildTypeId = transitiveDependency.GetMetadataValue( "CiBuildTypeId" );

                            if ( string.IsNullOrEmpty( buildNumber ) || string.IsNullOrEmpty( ciBuildTypeId ) )
                            {
                                context.Console.WriteError(
                                    $"The dependency '{name}' must have both BuildNumber and CiBuildTypeId properties in {directDependency.Source.VersionFile}." );

                                return false;
                            }

                            dependencySource = DependencySource.CreateBuildServerSource(
                                new CiBuildId( int.Parse( buildNumber, CultureInfo.InvariantCulture ), ciBuildTypeId ),
                                DependencyConfigurationOrigin.Transitive );

                            break;

                        case DependencySourceKind.Local:
                            dependencySource = DependencySource.CreateLocal( DependencyConfigurationOrigin.Transitive );

                            break;

                        case DependencySourceKind.Feed:
                            var version = transitiveDependency.GetMetadata( "Version" )?.EvaluatedValue;

                            if ( string.IsNullOrEmpty( version ) )
                            {
                                context.Console.WriteError( $"The dependency '{name}' must have a Version property in {directDependency.Source.VersionFile}." );

                                return false;
                            }

                            dependencySource = DependencySource.CreateFeed( version, DependencyConfigurationOrigin.Transitive );

                            break;

                        default:
                            throw new InvalidOperationException();
                    }

                    var newDependency = new Dependency( dependencySource, dependencyDefinition );
                    newDependenciesBuilder.Add( newDependency.Definition.Name, newDependency );
                    dependenciesOverrideFile.Dependencies[name] = dependencySource;
                }

                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            }

            newDependencies = newDependenciesBuilder.ToImmutable();

            return true;
        }

        private static bool ResolveBuildNumbersFromBranches(
            BuildContext context,
            BuildConfiguration configuration,
            TeamCityClient teamCity,
            ImmutableDictionary<string, Dependency> dependencies,
            bool update )
        {
            foreach ( var dependency in dependencies.Values )
            {
                if ( dependency.Source.SourceKind != DependencySourceKind.BuildServer )
                {
                    continue;
                }

                var buildSpec = dependency.Source.BuildServerSource;
                var buildId = buildSpec as CiBuildId;
                CiBuildId resolvedBuildId;

                if ( buildId != null && !update )
                {
                    resolvedBuildId = buildId;
                }
                else
                {
                    string ciBuildType;
                    string branchName;

                    if ( buildSpec is CiLatestBuildOfBranch branch )
                    {
                        ciBuildType = dependency.Definition.CiBuildTypes[configuration];
                        branchName = branch.Name;
                    }
                    else if ( buildId != null )
                    {
                        // We already have an resolved reference, but we need to update.
                        // In this case, we do not change the BuildIdType.

                        ciBuildType = buildId.BuildTypeId ?? dependency.Definition.CiBuildTypes[configuration];
                        var previousBranchName = teamCity.GetBranchFromBuildNumber( buildId, ConsoleHelper.CancellationToken );

                        if ( previousBranchName == null )
                        {
                            context.Console.WriteError( $"Cannot determine the branch for build {buildId}." );

                            return false;
                        }

                        branchName = previousBranchName;
                    }
                    else
                    {
                        ciBuildType = dependency.Definition.CiBuildTypes[configuration];
                        branchName = dependency.Definition.DefaultBranch;
                    }

                    var latestBuildNumber = teamCity.GetLatestBuildNumber(
                        ciBuildType,
                        branchName,
                        ConsoleHelper.CancellationToken );

                    if ( latestBuildNumber == null )
                    {
                        context.Console.WriteError(
                            $"No successful build for {dependency.Definition.Name} on branch {branchName} (BuildTypeId={ciBuildType}." );

                        return false;
                    }

                    resolvedBuildId = latestBuildNumber;
                }

                dependency.Source.BuildServerSource = resolvedBuildId;
            }

            return true;
        }

        private static bool DownloadArtifacts(
            BuildContext context,
            TeamCityClient teamCity,
            ImmutableDictionary<string, Dependency> dependencies )
        {
            foreach ( var dependency in dependencies.Values )
            {
                if ( dependency.Source.SourceKind != DependencySourceKind.BuildServer )
                {
                    // No need to download.
                    continue;
                }

                if ( dependency.Source.BuildServerSource is not CiBuildId buildId )
                {
                    // The dependency has not been resolved yet.
                    continue;
                }

                if ( buildId.BuildTypeId == null )
                {
                    context.Console.WriteError( $"The dependency '{dependency.Definition.Name}' does not have a Teamcity build type ID set." );

                    return false;
                }

                if ( !DownloadBuild(
                        context,
                        teamCity,
                        dependency.Source,
                        dependency.Definition.Name,
                        buildId.BuildTypeId,
                        buildId.BuildNumber ) )
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ResolveLocalDependencies( BuildContext context, ImmutableDictionary<string, Dependency> dependencies )
        {
            foreach ( var dependency in dependencies.Values.Where( d => d.Source.SourceKind == DependencySourceKind.Local ) )
            {
                if ( dependency.Source.VersionFile == null )
                {
                    dependency.Source.VersionFile = Path.GetFullPath(
                        Path.Combine(
                            context.RepoDirectory,
                            "..",
                            dependency.Definition.Name,
                            dependency.Definition.Name + ".Import.props" ) );
                }

                if ( !File.Exists( dependency.Source.VersionFile ) )
                {
                    context.Console.WriteError( $"The file '{dependency.Source.VersionFile}' does not exist. Check that the product has been built." );

                    return false;
                }
            }

            return true;
        }

        private static bool DownloadBuild(
            BuildContext context,
            TeamCityClient teamCity,
            DependencySource dependencySource,
            string dependencyName,
            string ciBuildTypeId,
            int buildNumber )
        {
            var restoreDirectory = Path.Combine(
                Environment.GetEnvironmentVariable( "USERPROFILE" ) ?? Path.GetTempPath(),
                ".build-artifacts",
                dependencyName,
                ciBuildTypeId,
                buildNumber.ToString( CultureInfo.InvariantCulture ) );

            var completedFile = Path.Combine( restoreDirectory, ".completed" );

            if ( !File.Exists( completedFile ) )
            {
                if ( Directory.Exists( restoreDirectory ) )
                {
                    Directory.Delete( restoreDirectory, true );
                }

                Directory.CreateDirectory( restoreDirectory );
                context.Console.WriteMessage( $"Downloading {dependencyName} build #{buildNumber} of {ciBuildTypeId}" );
                teamCity.DownloadArtifacts( ciBuildTypeId, buildNumber, restoreDirectory, ConsoleHelper.CancellationToken );

                File.WriteAllText( completedFile, "Completed" );
            }
            else
            {
                context.Console.WriteMessage( $"Dependency '{dependencyName}' is up to date: build #{buildNumber} of {ciBuildTypeId} was already downloaded." );
            }

            // Find the version file.
            var versionFile = FindVersionFile( dependencyName, restoreDirectory );

            if ( versionFile == null )
            {
                context.Console.WriteError( $"Could not find {dependencyName}.version.props under '{restoreDirectory}'." );

                return false;
            }

            dependencySource.VersionFile = versionFile;

            return true;
        }

        private static string? FindVersionFile( string productName, string directory )
        {
            var path = Path.Combine( directory, productName + ".version.props" );

            if ( File.Exists( path ) )
            {
                return path;
            }

            foreach ( var subdirectory in Directory.GetDirectories( directory ) )
            {
                path = FindVersionFile( productName, subdirectory );

                if ( path != null )
                {
                    return path;
                }
            }

            return null;
        }

        private record Dependency( DependencySource Source, DependencyDefinition Definition );
    }
}