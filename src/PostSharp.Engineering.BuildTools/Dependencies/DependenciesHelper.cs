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
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PostSharp.Engineering.BuildTools.Dependencies;

public static class DependenciesHelper
{
    public static bool UpdateOrFetchDependencies(
        BuildContext context,
        BuildConfiguration configuration,
        DependenciesOverrideFile dependenciesOverrideFile,
        bool update )
    {
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
            .Select( d => new ResolvedDependency( d.Value, d.Item2! ) )
            .ToList();

        if ( dependencies.Count == 0 )
        {
            context.Console.WriteWarning( "No dependencies to fetch." );

            return true;
        }

        TeamCityClient? tc;

        if ( !TeamCityHelper.TryConnectTeamCity( context, out tc ) )
        {
            return false;
        }

        var iterationDependencies = dependencies.ToImmutableDictionary( d => d.Dependency.Name, d => d );
        var dependencyDictionary = dependencies.ToImmutableDictionary( d => d.Dependency.Name, d => d );

        while ( iterationDependencies.Count > 0 )
        {
            if ( !ResolveBuildNumbersFromBranches( context, configuration, tc, iterationDependencies, update ) ||
                 !ResolveLocalDependencies( context, iterationDependencies ) ||
                 !ResolveRestoredDependencies( context, iterationDependencies ) )
            {
                return false;
            }

            // Download artefacts that are not transitive dependencies.
            if ( !DownloadArtifacts( context, configuration, tc, iterationDependencies ) )
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

        context.Console.WriteSuccess( $"{(update ? "Updating" : "Fetching")} build artifacts was successful" );

        return true;
    }

    private static bool TryGetTransitiveDependencies(
        BuildContext context,
        ImmutableDictionary<string, ResolvedDependency> allDependencies,
        ImmutableDictionary<string, ResolvedDependency> directDependencies,
        DependenciesOverrideFile dependenciesOverrideFile,
        [NotNullWhen( true )] out ImmutableDictionary<string, ResolvedDependency>? newDependencies )
    {
        var newDependenciesBuilder = ImmutableDictionary.CreateBuilder<string, ResolvedDependency>();

        newDependencies = null;

        foreach ( var directDependency in directDependencies.Values )
        {
            if ( directDependency.Source.SourceKind == DependencySourceKind.Feed )
            {
                // The dependency is managed by NuGet.
                // Currently, we don't support retrieving transitive dependencies from NuGet packages.
                continue;
            }

            var versionFile = Project.FromFile( directDependency.Source.VersionFile!, new ProjectOptions() );

            var transitiveDependencies = versionFile.Items.Where( i => i.ItemType == directDependency.Dependency.Definition.NameWithoutDot + "Dependencies" );

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

                if ( !context.Product.ProductFamily.TryGetDependencyDefinition( name, out var dependencyDefinition ) )
                {
                    context.Console.WriteError(
                        $"Cannot find the dependency definition for '{name}' referenced by '{directDependency.Dependency.Definition.Name}'. The dependency must be defined in PostSharp.Engineering." );

                    return false;
                }

                // Create a DependencySource.
                DependencySource dependencySource;

                bool TryGetBuildId( [NotNullWhen( true )] out CiBuildId? ciBuildId )
                {
                    var buildNumber = transitiveDependency.GetMetadataValue( "BuildNumber" );
                    var ciBuildTypeId = transitiveDependency.GetMetadataValue( "CiBuildTypeId" );

                    if ( string.IsNullOrEmpty( buildNumber ) || string.IsNullOrEmpty( ciBuildTypeId ) )
                    {
                        context.Console.WriteError(
                            $"The dependency '{name}' must have both BuildNumber and CiBuildTypeId properties in {directDependency.Source.VersionFile}." );

                        ciBuildId = null;

                        return false;
                    }

                    ciBuildId = new CiBuildId( int.Parse( buildNumber, CultureInfo.InvariantCulture ), ciBuildTypeId );

                    return true;
                }

                // If we build locally, we need to consider transitive restored dependencies as build server dependencies,
                // as dependencies are restored by CI on build agents only. Locally, all dependencies are downloaded by PostSharp.Engineering.
                if ( sourceKind == DependencySourceKind.RestoredDependency && directDependency.Source.SourceKind != DependencySourceKind.RestoredDependency )
                {
                    sourceKind = DependencySourceKind.BuildServer;
                }

                switch ( sourceKind )
                {
                    case DependencySourceKind.BuildServer:
                        {
                            if ( !TryGetBuildId( out var buildId ) )
                            {
                                return false;
                            }

                            dependencySource = DependencySource.CreateBuildServerSource(
                                buildId,
                                DependencyConfigurationOrigin.Transitive );
                        }

                        break;

                    case DependencySourceKind.Local:
                        dependencySource = DependencySource.CreateLocalRepo( DependencyConfigurationOrigin.Transitive );

                        break;

                    case DependencySourceKind.RestoredDependency:
                        {
                            if ( !TryGetBuildId( out var buildId ) )
                            {
                                return false;
                            }

                            if ( buildId.BuildTypeId == null )
                            {
                                context.Console.WriteError( $"Unknown build type ID of '{dependencyDefinition.Name}' transitive restored dependency." );
                            }

                            dependencySource = DependencySource.CreateRestoredDependency( buildId, DependencyConfigurationOrigin.Transitive );
                        }

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

                var newDependency = new ResolvedDependency( dependencySource, dependencyDefinition );
                newDependenciesBuilder.Add( newDependency.Dependency.Definition.Name, newDependency );
                dependenciesOverrideFile.Dependencies[name] = dependencySource;
            }

            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        newDependencies = newDependenciesBuilder.ToImmutable();

        return true;
    }

    private static bool GetLatestBuildId(
        ConsoleHelper console,
        TeamCityClient teamCity,
        string dependencyName,
        string ciBuildType,
        string branch,
        [NotNullWhen( true )] out CiBuildId? latestBuildId )
    {
        latestBuildId = teamCity.GetLatestBuildId(
            console,
            ciBuildType,
            branch );

        if ( latestBuildId == null )
        {
            console.WriteError( $"No successful build for '{dependencyName}' dependency on '{branch}' branch, BuildTypeId='{ciBuildType}'." );

            return false;
        }

        return true;
    }

    private static bool ResolveBuildNumbersFromBranches(
        BuildContext context,
        BuildConfiguration configuration,
        TeamCityClient teamCity,
        ImmutableDictionary<string, ResolvedDependency> dependencies,
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
                    ciBuildType = dependency.Dependency.Definition.CiConfiguration.BuildTypes[configuration];
                    branchName = branch.Name;
                }
                else if ( buildId != null )
                {
                    // We already have an resolved reference, but we need to update.
                    // In this case, we do not change the BuildIdType.

                    ciBuildType = buildId.BuildTypeId ?? dependency.Dependency.Definition.CiConfiguration.BuildTypes[configuration];

                    if ( !teamCity.TryGetBranchFromBuildNumber( context.Console, buildId, out var previousBranchName ) )
                    {
                        return false;
                    }

                    branchName = previousBranchName;
                }
                else
                {
                    ciBuildType = dependency.Dependency.Definition.CiConfiguration.BuildTypes[configuration];
                    branchName = dependency.Dependency.Definition.Branch;
                }

                if ( !GetLatestBuildId(
                        context.Console,
                        teamCity,
                        dependency.Dependency.Definition.Name,
                        ciBuildType,
                        branchName,
                        out var latestBuildId ) )
                {
                    return false;
                }

                resolvedBuildId = latestBuildId;
            }

            dependency.Source.BuildServerSource = resolvedBuildId;
        }

        return true;
    }

    private static bool DownloadArtifacts(
        BuildContext context,
        BuildConfiguration buildConfiguration,
        TeamCityClient teamCity,
        ImmutableDictionary<string, ResolvedDependency> dependencies )
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
                context.Console.WriteError( $"The dependency '{dependency.Dependency.Definition.Name}' does not have a Teamcity build type ID set." );

                return false;
            }

            var dependencyConfiguration = dependency.Dependency.ConfigurationMapping[buildConfiguration];
            var artifactsDirectory = dependency.Dependency.Definition.GetResolvedPrivateArtifactsDirectory( dependencyConfiguration );

            if ( !DownloadDependency(
                    context,
                    teamCity,
                    dependency.Source,
                    dependency.Dependency.Name,
                    buildId.BuildTypeId,
                    buildId.BuildNumber,
                    artifactsDirectory ) )
            {
                return false;
            }
        }

        return true;
    }

    private static bool ResolveLocalDependencies( BuildContext context, ImmutableDictionary<string, ResolvedDependency> dependencies )
    {
        foreach ( var dependency in dependencies.Values.Where( d => d.Source.SourceKind is DependencySourceKind.Local ) )
        {
            if ( dependency.Source.VersionFile == null )
            {
                dependency.Source.VersionFile = Path.GetFullPath(
                    Path.Combine(
                        context.RepoDirectory,
                        "..",
                        dependency.Dependency.Name,
                        dependency.Dependency.Name + ".Import.props" ) );
            }

            if ( !File.Exists( dependency.Source.VersionFile ) )
            {
                context.Console.WriteError( $"The file '{dependency.Source.VersionFile}' does not exist. Check that the product has been built." );

                return false;
            }
        }

        return true;
    }

    private static bool ResolveRestoredDependencies( BuildContext context, ImmutableDictionary<string, ResolvedDependency> dependencies )
    {
        foreach ( var dependency in dependencies.Values.Where( d => d.Source.SourceKind is DependencySourceKind.RestoredDependency ) )
        {
            if ( dependency.Source.SourceKind != DependencySourceKind.RestoredDependency )
            {
                continue;
            }

            if ( dependency.Source.VersionFile == null )
            {
                var path = Path.Combine( context.RepoDirectory, "dependencies", dependency.Dependency.Name, $"{dependency.Dependency.Name}.version.props" );
                dependency.Source.VersionFile = path;
            }

            if ( !File.Exists( dependency.Source.VersionFile ) )
            {
                context.Console.WriteError( $"The following artifact was not restored by TeamCity: '{dependency.Source.VersionFile}'" );

                return false;
            }

            var document = XDocument.Load( dependency.Source.VersionFile );

            var buildNumber = document.Root!.XPathSelectElement( $"/Project/PropertyGroup/{dependency.Dependency.Definition.NameWithoutDot}BuildNumber" )
                ?.Value;

            if ( buildNumber == null )
            {
                context.Console.WriteError(
                    $"The file '{dependency.Source.VersionFile}' does not have a property {dependency.Dependency.Definition.NameWithoutDot}BuildNumber" );

                return false;
            }

            var buildType = document.Root!.XPathSelectElement( $"/Project/PropertyGroup/{dependency.Dependency.Definition.NameWithoutDot}BuildType" )?.Value;

            if ( buildType == null )
            {
                context.Console.WriteError(
                    $"The file '{dependency.Source.VersionFile}' does not have a property {dependency.Dependency.Definition.NameWithoutDot}BuildType" );

                return false;
            }

            dependency.Source.BuildServerSource = new CiBuildId( int.Parse( buildNumber, CultureInfo.InvariantCulture ), buildType );
        }

        return true;
    }

    private static bool DownloadBuild(
        BuildContext context,
        TeamCityClient teamCity,
        string dependencyName,
        string ciBuildTypeId,
        int buildNumber,
        string artifactsDirectory,
        out string restoreDirectory )
    {
        restoreDirectory = Path.Combine(
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

            teamCity.DownloadArtifacts( context.Console, ciBuildTypeId, buildNumber, artifactsDirectory, restoreDirectory );

            File.WriteAllText( completedFile, "Completed" );
        }
        else
        {
            context.Console.WriteMessage( $"Dependency '{dependencyName}' is up to date: build #{buildNumber} of {ciBuildTypeId} was already downloaded." );
        }

        return true;
    }

    public static bool DownloadBuild(
        BuildContext context,
        TeamCityClient teamCity,
        BuildConfiguration configuration,
        string dependencyName,
        string ciBuildTypeId,
        int buildNumber )
    {
        var dependency = context.Product.GetDependency( dependencyName )!;
        var artifactsDirectory = dependency.GetPrivateArtifactsDirectory( configuration );

        return DownloadBuild(
            context,
            teamCity,
            dependencyName,
            ciBuildTypeId,
            buildNumber,
            artifactsDirectory,
            out _ );
    }

    private static readonly Regex _artifactsRuleRegex = new( @"^\+:(?<filter>[^=]+)=>(?<target>.+)$" );

    private static bool EmulateTeamCityArtifactRestore(
        BuildContext context,
        string dependencyName,
        string restoreDirectory,
        string artifactRules )
    {
        context.Console.WriteMessage( $"Emulating TeamCity artifacts restore of '{dependencyName}'." );

        foreach ( var artifactsRule in artifactRules.Split( ';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries ) )
        {
            context.Console.WriteMessage( $"Rule '{artifactsRule}':" );

            var artifactRulesMatch = _artifactsRuleRegex.Match( artifactsRule );

            if ( !artifactRulesMatch.Success )
            {
                context.Console.WriteError( $"Invalid artifact rule: {artifactsRule}" );

                return false;
            }

            var sourceFilter = artifactRulesMatch.Groups["filter"].Value;
            var targetDirectory = artifactRulesMatch.Groups["target"].Value.Replace( '/', Path.DirectorySeparatorChar );

            if ( !targetDirectory.StartsWith( $@"dependencies{Path.DirectorySeparatorChar}", StringComparison.Ordinal ) )
            {
                context.Console.WriteError( $"Invalid target directory of artifacts rule: {artifactsRule}" );
            }

            const string flatWildCard = "/*";
            const string recursiveWildCard = "/**/*";

            bool FilterToPath( string? wildcard, [NotNullWhen( true )] out string? path )
            {
                path = wildcard == null ? sourceFilter : sourceFilter.Substring( 0, sourceFilter.Length - wildcard.Length );
                path = path.Replace( '/', Path.DirectorySeparatorChar );

                if ( path.Contains( '*', StringComparison.Ordinal ) || path.Contains( '&', StringComparison.Ordinal ) )
                {
                    context.Console.WriteError( $"Path '{path}' contains unsupported wildcards." );

                    return false;
                }

                path = Path.Combine( restoreDirectory, path );

                return true;
            }

            IEnumerable<string> filesToCopy;

            if ( sourceFilter.EndsWith( recursiveWildCard, StringComparison.Ordinal ) )
            {
                if ( !FilterToPath( recursiveWildCard, out var sourcePath ) )
                {
                    return false;
                }

                filesToCopy = Directory.EnumerateFiles( sourcePath, "*", SearchOption.TopDirectoryOnly );
            }
            else if ( sourceFilter.EndsWith( flatWildCard, StringComparison.Ordinal ) )
            {
                if ( !FilterToPath( flatWildCard, out var sourcePath ) )
                {
                    return false;
                }

                filesToCopy = Directory.EnumerateFiles( sourcePath, "*", SearchOption.TopDirectoryOnly );
            }
            else
            {
                if ( !FilterToPath( null, out var sourcePath ) )
                {
                    return false;
                }

                filesToCopy = new[] { sourcePath };
            }

            foreach ( var sourceFile in filesToCopy )
            {
                var fileName = Path.GetFileName( sourceFile );
                var targetFile = Path.Combine( context.RepoDirectory, targetDirectory, fileName );
                var absoluteTargetDirectory = Path.GetDirectoryName( targetFile )!;

                Directory.CreateDirectory( absoluteTargetDirectory );

                context.Console.WriteMessage(
                    $"{Path.GetRelativePath( restoreDirectory, sourceFile )} -> {Path.GetRelativePath( context.RepoDirectory, targetFile )}" );

                File.Copy( sourceFile, targetFile );
            }
        }

        context.Console.WriteMessage( $"TeamCity artifacts restore emulation of '{dependencyName}' succeeded." );

        return true;
    }

    private static bool DownloadDependency(
        BuildContext context,
        TeamCityClient teamCity,
        DependencySource dependencySource,
        string dependencyName,
        string ciBuildTypeId,
        int buildNumber,
        string artifactsDirectory )
    {
        if ( !DownloadBuild( context, teamCity, dependencyName, ciBuildTypeId, buildNumber, artifactsDirectory, out var restoreDirectory ) )
        {
            return false;
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

    private static bool TryGetArtifactRules(
        TeamCityClient tc,
        ConsoleHelper console,
        string ciBuildTypeId,
        [NotNullWhen( true )] out ImmutableDictionary<string, string>? artifactRules )
    {
        console.WriteMessage( $"Fetching build configuration of '{ciBuildTypeId}' build type." );

        if ( !tc.TryGetBuildTypeConfiguration( console, ciBuildTypeId, out var teamCityBuildConfiguration ) )
        {
            artifactRules = null;

            return false;
        }

        artifactRules = teamCityBuildConfiguration
            .Root
            !.Element( "artifact-dependencies" )!
            .Elements( "artifact-dependency" )
            .ToImmutableDictionary(
                d => d.Element( "source-buildType" )!.Attribute( "id" )!.Value,
                d => d.Element( "properties" )!.Elements( "property" ).Single( p => p.Attribute( "name" )!.Value == "pathRules" ).Attribute( "value" )!
                    .Value );

        return true;
    }

    private record ResolvedDependency( DependencySource Source, ParametrizedDependency Dependency );
}