using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

            var versionsOverrideFile = VersionsOverrideFile.Load( context );

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
                .Select( d => (d.Value, GetDependencyDefinition( d )) )
                .Where( d => d.Item2 != null )
                .Select( d => new Dependency( d.Value, d.Item2! ) )
                .ToList()!;

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

            var iterationDependencies = dependencies;

            while ( iterationDependencies.Count > 0 )
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
                if ( !TryGetTransitiveDependencies( context, iterationDependencies, versionsOverrideFile, out var allDependencies ) )
                {
                    return false;
                }

                // Resolve transitive dependencies from artefacts that have been downloaded.
                if ( !ResolveTransitiveDependencies( context, allDependencies, versionsOverrideFile, out var resolvedDependencies ) )
                {
                    return false;
                }

                iterationDependencies = resolvedDependencies;
            }

            context.Console.WriteSuccess( "Fetching build artifacts was successful" );

            return true;
        }

        private static bool TryGetTransitiveDependencies(
            BuildContext context,
            List<Dependency> directDependencies,
            VersionsOverrideFile versionsOverrideFile,
            [NotNullWhen( true )] out List<Dependency>? allDependencies )
        {
            var dependencies = directDependencies.ToDictionary( d => d.Definition.Name, d => d );

            foreach ( var directDependency in directDependencies )
            {
                switch ( directDependency.Source.SourceKind )
                {
                    case DependencySourceKind.Transitive:
                        // There is no need to resolve dependencies deployed to a public source because nuget does it.
                        continue;

                    case DependencySourceKind.Default:
                        // Defining transitive dependencies explicitly is now obsolete, but we can still get here 
                        // because of a previous iteration.
                        continue;
                }

                var versionFile = Project.FromFile( directDependency.Source.VersionFile!, new ProjectOptions() );

                var transitiveDependencies = versionFile.Items.Where( i => i.ItemType == directDependency.Definition.Name + "Dependencies" );

                foreach ( var transitiveDependency in transitiveDependencies )
                {
                    var name = transitiveDependency.EvaluatedInclude;
                    var sourceKind = Enum.Parse<DependencySourceKind>( transitiveDependency.GetMetadata( "SourceKind" )!.EvaluatedValue );

                    var version = transitiveDependency.GetMetadata( "DefaultVersion" )?.EvaluatedValue;

                    if ( dependencies.TryGetValue( name, out _ ) )
                    {
                        // We don't expect conflicts, neither try to resolve them.   
                    }
                    else
                    {
                        // Get the DependencyDefinition.
                        var dependencyDefinition = Model.Dependencies.All.SingleOrDefault( d => d.Name == name );

                        if ( dependencyDefinition == null )
                        {
                            context.Console.WriteError(
                                $"Cannot find the dependency definition for '{name}' referenced by '{directDependency.Definition.Name}'. The dependency should be defined in PostSharp.Engineering." );

                            allDependencies = null;

                            return false;
                        }

                        // Create a DependencySource.
                        DependencySource dependencySource;

                        switch ( sourceKind )
                        {
                            case DependencySourceKind.BuildServer:
                            case DependencySourceKind.Transitive:
                                dependencySource = DependencySource.CreateTransitiveBuildServerSource(
                                    directDependency.Definition.Name,
                                    version,
                                    directDependency.Definition.Name );

                                break;

                            case DependencySourceKind.Local:
                                dependencySource = DependencySource.CreateOfKind( DependencySourceKind.Local, directDependency.Definition.Name );

                                break;

                            case DependencySourceKind.Default:
                                // Nothing to do because the dependency is published on a public nuget source.
                                continue;

                            default:
                                throw new InvalidOperationException();
                        }

                        dependencies[name] = new Dependency( dependencySource, dependencyDefinition );
                        versionsOverrideFile.Dependencies[name] = dependencySource;
                    }
                }

                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            }

            allDependencies = dependencies.Values.ToList();

            return true;
        }

        private static bool ResolveBuildNumbersFromBranches(
            BuildContext context,
            TeamcityClient teamcity,
            List<Dependency> dependencies,
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
            List<Dependency> dependencies )
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

        private static bool ResolveLocalDependencies( BuildContext context, List<Dependency> dependencies )
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

        private static bool ResolveTransitiveDependencies(
            BuildContext context,
            List<Dependency> dependencies,
            VersionsOverrideFile versionsOverrideFile,
            [NotNullWhen( true )] out List<Dependency>? resolvedDependencies )
        {
            resolvedDependencies = new List<Dependency>();

            foreach ( var dependency in dependencies )
            {
                if ( dependency.Source.SourceKind != DependencySourceKind.Transitive )
                {
                    continue;
                }

                var dependencyName = dependency.Definition.Name;

                if ( dependency.Source.VersionDefiningDependencyName == null )
                {
                    context.Console.WriteError( $"The dependency '{dependencyName}' does not have a version defining dependency name set." );

                    return false;
                }

                var versionDefiningDependencyName = dependency.Source.VersionDefiningDependencyName;

                if ( !versionsOverrideFile.Dependencies.TryGetValue( versionDefiningDependencyName, out var versionDefiningDependency ) )
                {
                    context.Console.WriteError(
                        $"Version defining dependency '{versionDefiningDependencyName}' of the dependency '{dependencyName}' not found." );

                    return false;
                }

                if ( versionDefiningDependency.VersionFile == null && versionDefiningDependency.SourceKind == DependencySourceKind.Local )
                {
                    var versionDefiningDependencyDefinition = context.Product.GetDependency( versionDefiningDependencyName );

                    if ( versionDefiningDependencyDefinition == null )
                    {
                        context.Console.WriteError(
                            $"Version defining dependency definition '{versionDefiningDependencyName}' of the dependency '{dependencyName}' not found." );

                        return false;
                    }

                    var versionDefiningDependencyLocalImportProjectPath = Path.Combine(
                        context.RepoDirectory,
                        "..",
                        versionDefiningDependencyDefinition.RepoName,
                        $"{versionDefiningDependencyName}.Import.props" );

                    var versionDefiningDependencyLocalImportProject = Project.FromFile( versionDefiningDependencyLocalImportProjectPath, new ProjectOptions() );

                    var versionDefiningDependencyVersionFileName = $"{versionDefiningDependencyName}.version.props";

                    var versionDefiningDependencyVersionFilePath = versionDefiningDependencyLocalImportProject.Imports
                        .Select( i => i.ImportedProject.FullPath )
                        .SingleOrDefault( p => Path.GetFileName( p ) == versionDefiningDependencyVersionFileName );

                    ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

                    versionDefiningDependency.VersionFile = versionDefiningDependencyVersionFilePath;
                }

                if ( versionDefiningDependency.VersionFile == null )
                {
                    context.Console.WriteError(
                        $"The version file of the version defining dependency '{versionDefiningDependencyName}' of the dependency '{dependencyName}' is unknown." );

                    return false;
                }

                var versionDefiningDependencyVersionFile = Project.FromFile( versionDefiningDependency.VersionFile, new ProjectOptions() );

                var dependencyVersionItem = versionDefiningDependencyVersionFile.GetItemsByEvaluatedInclude( dependencyName ).SingleOrDefault();

                if ( dependencyVersionItem == null )
                {
                    context.Console.WriteError(
                        $"The version file of the version defining dependency '{versionDefiningDependencyName}' of the dependency '{dependencyName}' doesn't contain the corresponding dependency item." );

                    return false;
                }

                var dependencySettings = dependencyVersionItem.DirectMetadata.ToDictionary( m => m.Name, m => m.EvaluatedValue );

                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

                bool TryGetSettingsValue( string name, [NotNullWhen( true )] out string? value )
                {
                    if ( !dependencySettings!.TryGetValue( name, out value ) || string.IsNullOrWhiteSpace( value ) )
                    {
                        context.Console.WriteError(
                            $"The dependency item of the version defining dependency '{versionDefiningDependencyName}' of the dependency '{dependencyName}' doesn't contain metadata '{name}'." );

                        return false;
                    }

                    return true;
                }

                if ( !TryGetSettingsValue( "SourceKind", out var sourceKindString ) )
                {
                    return false;
                }

                var sourceKind = Enum.Parse<DependencySourceKind>( sourceKindString );

                switch ( sourceKind )
                {
                    case DependencySourceKind.Local:
                        dependency.Source.SourceKind = DependencySourceKind.Local;

                        resolvedDependencies.Add( dependency );

                        break;

                    case DependencySourceKind.Default:
                    case DependencySourceKind.Transitive:
                        dependency.Source.SourceKind = DependencySourceKind.Transitive;

                        if ( !TryGetSettingsValue( "DefaultVersion", out var defaultVersion ) )
                        {
                            return false;
                        }

                        dependency.Source.DefaultVersion = defaultVersion;

                        break;

                    case DependencySourceKind.BuildServer:

                        resolvedDependencies.Add( dependency );

                        dependency.Source.SourceKind = DependencySourceKind.BuildServer;

                        if ( !TryGetSettingsValue( "BuildNumber", out var buildNumberString ) )
                        {
                            return false;
                        }

                        var buildNumber = int.Parse( buildNumberString, CultureInfo.InvariantCulture );
                        dependency.Source.BuildNumber = buildNumber;

                        if ( !TryGetSettingsValue( "CiBuildTypeId", out var ciBuildTypeId ) )
                        {
                            return false;
                        }

                        dependency.Source.CiBuildTypeId = ciBuildTypeId;

                        if ( dependencySettings.TryGetValue( "Branch", out var branch ) && !string.IsNullOrWhiteSpace( branch ) )
                        {
                            dependency.Source.Branch = branch;
                        }

                        break;

                    default:
                        context.Console.WriteError(
                            $"The dependency item of the version defining dependency '{versionDefiningDependencyName}' of the dependency '{dependencyName}' contains invalid source kind '{sourceKind}'." );

                        return false;
                }
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