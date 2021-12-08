using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    public class FetchDependencyCommand : BaseCommand<FetchDependenciesCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, FetchDependenciesCommandSettings settings )
        {
            context.Console.WriteHeading( "Fetching build artefacts" );

            var versionsOverrideFile = VersionsOverrideFile.Load( context );

            return FetchDependencies( context, versionsOverrideFile );
        }

        public static bool FetchDependencies( BuildContext context, VersionsOverrideFile versionsOverrideFile )
        {
            var buildServerDependencies = versionsOverrideFile.Dependencies.Where( d => d.Value.SourceKind == DependencySourceKind.BuildServer ).ToList();

            if ( buildServerDependencies.Count == 0 )
            {
                context.Console.WriteWarning( "No dependency to fetch." );

                return true;
            }

            var token = Environment.GetEnvironmentVariable( "TEAMCITY_TOKEN" );

            if ( string.IsNullOrEmpty( token ) )
            {
                context.Console.WriteError( "The TEAMCITY_TOKEN environment variable is not defined." );

                return false;
            }

            var teamcity = new TeamcityClient( token );

            foreach ( var dependency in buildServerDependencies )
            {
                var dependencyDefinition = context.Product.Dependencies.SingleOrDefault( p => p.Name == dependency.Key );

                if ( dependencyDefinition == null )
                {
                    context.Console.WriteError( $"The dependency '{dependency.Key}' is not added to the product." );

                    return false;
                }

                var branch = dependency.Value.Branch ?? dependencyDefinition.DefaultBranch;

                if ( dependencyDefinition.CiBuildTypeId == null )
                {
                    context.Console.WriteError( $"The dependency '{dependency.Key}' does not have a Teamcity configuration." );

                    return false;
                }

                var buildNumber = teamcity.GetLatestBuildNumber(
                    dependencyDefinition.CiBuildTypeId,
                    branch,
                    ConsoleHelper.CancellationToken );

                if ( buildNumber == null )
                {
                    context.Console.WriteError( $"No successful build for {dependency.Key} on branch {branch}." );

                    return false;
                }

                var restoreDirectory = dependencyDefinition.GetLocalDirectory( buildNumber.Value );

                var completedFile = Path.Combine( restoreDirectory, ".completed" );

                if ( !File.Exists( completedFile ) )
                {

                    if ( Directory.Exists( restoreDirectory ) )
                    {
                        Directory.Delete( restoreDirectory, true );
                    }

                    Directory.CreateDirectory( restoreDirectory );
                    context.Console.WriteMessage( $"Downloading {dependency.Key} build {buildNumber}" );
                    teamcity.DownloadArtifacts( dependencyDefinition.CiBuildTypeId, buildNumber.Value, restoreDirectory, ConsoleHelper.CancellationToken );

                    File.WriteAllText( completedFile, "Completed" );
                }
                else
                {
                    context.Console.WriteMessage( $"{dependency.Key} is up to date." );
                }

                // Find the version file.
                var versionFile = FindVersionFile( dependency.Key, restoreDirectory );

                if ( versionFile == null )
                {
                    context.Console.WriteError( $"Could not find {dependency.Key}.version.props under '{restoreDirectory}'." );

                    return false;
                }

                dependency.Value.VersionFile = versionFile;
            }

            context.Console.WriteSuccess( "Fetching build artefacts was successful" );

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