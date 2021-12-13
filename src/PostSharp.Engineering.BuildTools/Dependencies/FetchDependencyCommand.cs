using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
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

            return FetchDependencies( context, versionsOverrideFile );
        }

        public static bool FetchDependencies( BuildContext context, VersionsOverrideFile versionsOverrideFile )
        {
            DependencyDefinition? GetDependencyDefinition(KeyValuePair<string, DependencySource> dependency)
            {
                var dependencyDefinition = context.Product.Dependencies.SingleOrDefault( d => d.Name == dependency.Key );

                if ( dependencyDefinition == null )
                {
                    context.Console.WriteWarning( $"The dependency '{dependency.Key}' is not configured. Ignoring." );
                }

                return dependencyDefinition;
            }

            List<(DependencySource Source, DependencyDefinition Definition)> GetDependencies( bool requireBuildNumber )
                => versionsOverrideFile
                .Dependencies
                .Where( d => d.Value.SourceKind == DependencySourceKind.BuildServer && d.Value.BuildNumber.HasValue == requireBuildNumber )
                .Select( d => (d.Value, GetDependencyDefinition( d )) )
                .Where( d => d.Item2 != null ).ToList()!;

            var buildServerBranchDependencies = GetDependencies( requireBuildNumber: false );
            var buildServerBuildDependencies = GetDependencies( requireBuildNumber: true );

            if ( buildServerBranchDependencies.Count + buildServerBuildDependencies.Count == 0 )
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

            if ( !FetchBuildServerBuildDependencies( context, teamcity, buildServerBuildDependencies ) )
            {
                return false;
            }

            if ( !FetchBuildServerBranchDependencies( context, teamcity, buildServerBranchDependencies ) )
            {
                return false;
            }

            context.Console.WriteSuccess( "Fetching build artifacts was successful" );

            return true;
        }

        private static bool FetchBuildServerBuildDependencies( BuildContext context, TeamcityClient teamcity, List<(DependencySource Source, DependencyDefinition Definition)> dependencies )
        {
            foreach ( var dependency in dependencies )
            {
                // Nullability checked by the colection filter above.
                var buildNumber = (int) dependency.Source.BuildNumber!;

                var ciBuildTypeId = dependency.Source.CiBuildTypeId;

                if ( ciBuildTypeId == null )
                {
                    ciBuildTypeId = dependency.Definition.CiBuildTypeId;
                }

                if ( ciBuildTypeId == null )
                {
                    context.Console.WriteError( $"The dependency '{dependency.Definition.Name}' does not have a Teamcity build type ID set." );

                    return false;
                }

                if ( !FetchBuild( context, teamcity, dependency.Source, dependency.Definition.Name, dependency.Definition.RepoName, ciBuildTypeId, buildNumber ) )
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FetchBuildServerBranchDependencies( BuildContext context, TeamcityClient teamcity, List<(DependencySource Source, DependencyDefinition Definition)> dependencies )
        {
            foreach ( var dependency in dependencies )
            { 
                var branch = dependency.Source.Branch ?? dependency.Definition.DefaultBranch;

                if ( dependency.Definition.CiBuildTypeId == null )
                {
                    context.Console.WriteError( $"The dependency '{dependency.Definition.Name}' does not have a Teamcity build type ID set." );

                    return false;
                }

                var buildNumber = teamcity.GetLatestBuildNumber(
                    dependency.Definition.CiBuildTypeId,
                    branch,
                    ConsoleHelper.CancellationToken );

                if ( buildNumber == null )
                {
                    context.Console.WriteError(
                        $"No successful build for {dependency.Definition.Name} on branch {branch} (BuildTypeId={dependency.Definition.CiBuildTypeId}." );

                    return false;
                }

                if ( !FetchBuild( context, teamcity, dependency.Source, dependency.Definition.Name, dependency.Definition.RepoName, dependency.Definition.CiBuildTypeId, buildNumber.Value ) )
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FetchBuild(
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
                ciBuildTypeId!,
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
                context.Console.WriteMessage( $"{dependencyName} is up to date." );
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
    }
}