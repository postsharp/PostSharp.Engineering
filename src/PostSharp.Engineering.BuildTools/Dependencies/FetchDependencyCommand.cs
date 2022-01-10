using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    public class FetchDependencyCommand : BaseCommand<FetchDependenciesCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, FetchDependenciesCommandSettings settings )
        {
            context.Console.WriteHeading( "Fetching build artifacts" );

            if ( !VersionsOverrideFile.TryLoad( context, out var versionsOverrideFile ) )
            {
                return false;
            }

            if ( !FetchDependencies( context, versionsOverrideFile, settings ) )
            {
                return false;
            }

            if ( !versionsOverrideFile.TrySave( context ) )
            {
                return false;
            }

            return true;
        }

        public static bool FetchDependencies(
            BuildContext context,
            VersionsOverrideFile versionsOverrideFile,
            FetchDependenciesCommandSettings? settings = null )
        {
            settings ??= new FetchDependenciesCommandSettings();

            DependencyDefinition? GetDependencyDefinition( KeyValuePair<string, DependencySource> dependency )
            {
                var dependencyDefinition = context.Product.GetDependency( dependency.Key );

                if ( dependencyDefinition == null )
                {
                    context.Console.WriteWarning( $"The dependency '{dependency.Key}' is not configured. Ignoring." );
                }

                return dependencyDefinition;
            }

            var dependencies = versionsOverrideFile
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

            var teamcity = new TeamcityClient( token );

            var iterationDependencies = dependencies.ToImmutableArray();
            var dependencyDictionary = dependencies.ToImmutableDictionary( d => d.Definition.Name, d => d );

            while ( iterationDependencies.Length > 0 )
            {
                // Download artefacts that are not transitive dependencies.
                if ( !ResolveBuildNumbersFromBranches( context, teamcity, iterationDependencies, settings.Update ) ||
                     !ResolveLocalDependencies( context, iterationDependencies ) )
                {
                    return false;
                }

                if ( !DownloadArtifacts( context, teamcity, iterationDependencies ) )
                {
                    return false;
                }

                // Find implicit transitive dependencies.
                if ( !TryGetTransitiveDependencies( context, dependencyDictionary, iterationDependencies, versionsOverrideFile, out var newDependencies ) )
                {
                    return false;
                }

                iterationDependencies = newDependencies;

                dependencyDictionary =
                    dependencyDictionary.AddRange( newDependencies.Select( d => new KeyValuePair<string, Dependency>( d.Definition.Name, d ) ) );
            }

            context.Console.WriteSuccess( "Fetching build artifacts was successful" );

            return true;
        }

        private static bool TryGetTransitiveDependencies(
            BuildContext context,
            ImmutableDictionary<string, Dependency> allDependencies,
            ImmutableArray<Dependency> directDependencies,
            VersionsOverrideFile versionsOverrideFile,
            out ImmutableArray<Dependency> newDependencies )
        {
            var newDependenciesBuilder = ImmutableArray.CreateBuilder<Dependency>();

            newDependencies = default;

            foreach ( var directDependency in directDependencies )
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
                    var dependencyDefinition = Model.Dependencies.All.SingleOrDefault( d => d.Name == name );

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
                            dependencySource = DependencySource.CreateBuildServerSource(
                                directDependency.Source.Branch!,
                                directDependency.Source.CiBuildTypeId,
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
                    newDependenciesBuilder.Add( newDependency );
                    versionsOverrideFile.Dependencies[name] = dependencySource;
                }

                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            }

            newDependencies = newDependenciesBuilder.ToImmutable();

            return true;
        }

        private static bool ResolveBuildNumbersFromBranches(
            BuildContext context,
            TeamcityClient teamcity,
            ImmutableArray<Dependency> dependencies,
            bool update )
        {
            foreach ( var dependency in dependencies )
            {
                if ( dependency.Source.SourceKind != DependencySourceKind.BuildServer || (!update && dependency.Source.BuildNumber != null) )
                {
                    continue;
                }

                var branch = dependency.Source.Branch ?? dependency.Definition.DefaultBranch;

                if ( dependency.Definition.DefaultCiBuildTypeId == null )
                {
                    context.Console.WriteError( $"The dependency '{dependency.Definition.Name}' does not have a Teamcity build type ID set." );

                    return false;
                }

                var buildNumber = teamcity.GetLatestBuildNumber(
                    dependency.Definition.DefaultCiBuildTypeId,
                    branch,
                    ConsoleHelper.CancellationToken );

                if ( buildNumber == null )
                {
                    context.Console.WriteError(
                        $"No successful build for {dependency.Definition.Name} on branch {branch} (BuildTypeId={dependency.Definition.DefaultCiBuildTypeId}." );

                    return false;
                }

                dependency.Source.BuildNumber = buildNumber;
                dependency.Source.CiBuildTypeId = dependency.Definition.DefaultCiBuildTypeId;
            }

            return true;
        }

        private static bool DownloadArtifacts(
            BuildContext context,
            TeamcityClient teamcity,
            ImmutableArray<Dependency> dependencies )
        {
            foreach ( var dependency in dependencies )
            {
                if ( dependency.Source.SourceKind != DependencySourceKind.BuildServer || dependency.Source.BuildNumber == null )
                {
                    continue;
                }

                var buildNumber = (int) dependency.Source.BuildNumber;

                var ciBuildTypeId = dependency.Source.CiBuildTypeId;

                if ( ciBuildTypeId == null )
                {
                    ciBuildTypeId = dependency.Definition.DefaultCiBuildTypeId;
                }

                if ( ciBuildTypeId == null )
                {
                    context.Console.WriteError( $"The dependency '{dependency.Definition.Name}' does not have a Teamcity build type ID set." );

                    return false;
                }

                if ( !DownloadBuild(
                        context,
                        teamcity,
                        dependency.Source,
                        dependency.Definition.Name,
                        dependency.Definition.RepoName,
                        ciBuildTypeId,
                        buildNumber ) )
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ResolveLocalDependencies( BuildContext context, ImmutableArray<Dependency> dependencies )
        {
            foreach ( var dependency in dependencies.Where( d => d.Source.SourceKind == DependencySourceKind.Local ) )
            {
                var importsFile = Path.GetFullPath(
                    Path.Combine(
                        context.RepoDirectory,
                        "..",
                        dependency.Definition.Name,
                        dependency.Definition.Name + ".Import.props" ) );

                if ( !File.Exists( importsFile ) )
                {
                    context.Console.WriteError( $"The file '{importsFile}' does not exist. Check that the product has been built." );

                    return false;
                }

                dependency.Source.VersionFile = importsFile;
            }

            return true;
        }

        private static bool DownloadBuild(
            BuildContext context,
            TeamcityClient teamcity,
            DependencySource dependencySource,
            string dependencyName,
            string repoName,
            string ciBuildTypeId,
            int buildNumber )
        {
            var restoreDirectory = Path.Combine(
                Environment.GetEnvironmentVariable( "USERPROFILE" ) ?? Path.GetTempPath(),
                ".build-artifacts",
                repoName,
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
                teamcity.DownloadArtifacts( ciBuildTypeId, buildNumber, restoreDirectory, ConsoleHelper.CancellationToken );

                File.WriteAllText( completedFile, "Completed" );
            }
            else
            {
                context.Console.WriteMessage( $"Dependency '{dependencyName}' is up to date." );
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