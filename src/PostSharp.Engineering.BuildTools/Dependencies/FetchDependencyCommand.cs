using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    public class FetchDependencyCommand : BaseCommand<FetchDependenciesCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, FetchDependenciesCommandSettings options )
        {
            context.Console.WriteHeading( "Fetching build artefacts" );

            var versionsOverrideFile = VersionsOverrideFile.Load( context );
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
                var vcsInfo = context.Product.Dependencies.SingleOrDefault( p => p.Name == dependency.Key );

                if ( vcsInfo == null )
                {
                    context.Console.WriteError( $"The dependency '{dependency.Key}' is not added to the product." );

                    return false;
                }

                var branch = dependency.Value.Branch ?? vcsInfo.DefaultBranch;

                if ( vcsInfo.CiBuildTypeId == null )
                {
                    context.Console.WriteError( $"The dependency '{dependency.Key}' does not have a Teamcity configuration." );

                    return false;
                }

                var buildNumber = teamcity.GetLatestBuildNumber(
                    vcsInfo.CiBuildTypeId,
                    branch,
                    ConsoleHelper.CancellationToken );

                if ( buildNumber == null )
                {
                    context.Console.WriteError( $"No successful build for {dependency.Key} on branch {branch}." );

                    return false;
                }

                var restoreDirectory = Path.Combine( context.RepoDirectory, "artifacts", "dependencies", dependency.Key );

                var versionFile = Path.Combine( restoreDirectory, ".version" );

                if ( File.Exists( versionFile ) )
                {
                    var version = int.Parse( File.ReadAllText( versionFile ).Trim(), CultureInfo.InvariantCulture );

                    if ( version >= buildNumber )
                    {
                        context.Console.WriteMessage( $"{dependency.Key} is up to date." );

                        continue;
                    }
                }

                if ( Directory.Exists( restoreDirectory ) )
                {
                    Directory.Delete( restoreDirectory, true );
                }

                Directory.CreateDirectory( restoreDirectory );
                context.Console.WriteMessage( $"Downloading {dependency.Key} build {buildNumber}" );
                teamcity.DownloadArtifacts( vcsInfo.CiBuildTypeId, buildNumber.Value, restoreDirectory, ConsoleHelper.CancellationToken );

                File.WriteAllText( versionFile, buildNumber.ToString() );
            }

            context.Console.WriteSuccess( "Fetching build artefacts was successful" );

            return true;
        }
    }
}