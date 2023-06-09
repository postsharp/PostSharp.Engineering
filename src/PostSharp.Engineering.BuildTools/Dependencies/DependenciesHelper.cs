﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;

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
            .Select( d => new Dependency( d.Value, d.Item2! ) )
            .ToList();

        if ( dependencies.Count == 0 )
        {
            context.Console.WriteWarning( "No dependencies to fetch." );

            return true;
        }

        if ( !TeamCityHelper.TryConnectTeamCity( context, out var tc ) )
        {
            return false;
        }

        var iterationDependencies = dependencies.ToImmutableDictionary( d => d.Definition.Name, d => d );
        var dependencyDictionary = dependencies.ToImmutableDictionary( d => d.Definition.Name, d => d );

        while ( iterationDependencies.Count > 0 )
        {
            if ( !ResolveBuildNumbersFromBranches( context, configuration, tc, iterationDependencies, update ) ||
                 !ResolveLocalDependencies( context, iterationDependencies ) ||
                 !ResolveRestoredDependencies( context, iterationDependencies ) )
            {
                return false;
            }

            // Download artefacts that are not transitive dependencies.
            if ( !DownloadArtifacts( context, tc, iterationDependencies ) )
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
                // Currently, we don't support retrieving transitive dependencies from NuGet packages.
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
                var dependencyDefinition = context.Product.ProductFamily.GetDependencyDefinitionOrNull( name );

                if ( dependencyDefinition == null )
                {
                    context.Console.WriteError(
                        $"Cannot find the dependency definition for '{name}' referenced by '{directDependency.Definition.Name}'. The dependency must be defined in PostSharp.Engineering." );

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
                    ciBuildType = dependency.Definition.CiConfiguration.BuildTypes[configuration];
                    branchName = branch.Name;
                }
                else if ( buildId != null )
                {
                    // We already have an resolved reference, but we need to update.
                    // In this case, we do not change the BuildIdType.

                    ciBuildType = buildId.BuildTypeId ?? dependency.Definition.CiConfiguration.BuildTypes[configuration];

                    if ( !teamCity.TryGetBranchFromBuildNumber( context.Console, buildId, out var previousBranchName ) )
                    {
                        return false;
                    }

                    branchName = previousBranchName;
                }
                else
                {
                    ciBuildType = dependency.Definition.CiConfiguration.BuildTypes[configuration];
                    branchName = dependency.Definition.Branch;
                }

                if ( context.Product.VcsProvider == null )
                {
                    throw new InvalidOperationException( "VCS provider is missing for the product." );
                }

                var isDefaultBranch = branchName == dependency.Definition.Branch;

                var latestBuildNumber = teamCity.GetLatestBuildNumber(
                    ciBuildType,
                    branchName,
                    isDefaultBranch );

                if ( latestBuildNumber == null )
                {
                    context.Console.WriteError(
                        $"No successful build for '{dependency.Definition.Name}' dependency on '{branchName}' branch, BuildTypeId='{ciBuildType}', IsDefaultBranch='{isDefaultBranch}'." );

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
            if ( dependency.Source.SourceKind is not DependencySourceKind.BuildServer )
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
        foreach ( var dependency in dependencies.Values.Where( d => d.Source.SourceKind is DependencySourceKind.Local ) )
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

    private static bool ResolveRestoredDependencies( BuildContext context, ImmutableDictionary<string, Dependency> dependencies )
    {
        foreach ( var dependency in dependencies.Values.Where( d => d.Source.SourceKind is DependencySourceKind.RestoredDependency ) )
        {
            if ( dependency.Source.VersionFile == null )
            {
                var path = Path.Combine( context.RepoDirectory, "dependencies", dependency.Definition.Name, $"{dependency.Definition.Name}.version.props" );
                dependency.Source.VersionFile = path;
            }

            if ( !File.Exists( dependency.Source.VersionFile ) )
            {
                context.Console.WriteError( $"The file '{dependency.Source.VersionFile}' does not exist. Check that the dependency has been restored." );

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
            teamCity.DownloadArtifacts( ciBuildTypeId, buildNumber, restoreDirectory );

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