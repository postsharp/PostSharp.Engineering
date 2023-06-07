// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Newtonsoft.Json;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Utilities;
using System.IO;
using System.Text.RegularExpressions;

namespace PostSharp.Engineering.BuildTools.Build.Solutions
{
    /// <summary>
    /// An implementation of <see cref="Solution"/> that uses the <c>dotnet</c> utility to build projects.
    /// </summary>
    public class DotNetSolution : Solution
    {
        public DotNetSolution( string solutionPath ) : base( solutionPath ) { }

        public override bool Build( BuildContext context, BuildSettings settings ) => this.RunDotNet( context, settings, "build", "", true );

        public override bool Pack( BuildContext context, BuildSettings settings ) => this.RunDotNet( context, settings, "pack", "", true );

        public override bool Test( BuildContext context, BuildSettings settings )
        {
            var resultsRelativeDirectory =
                context.Product.TestResultsDirectory.ToString( new BuildInfo( null!, settings.BuildConfiguration, context.Product ) );

            var resultsDirectory = Path.Combine( context.RepoDirectory, resultsRelativeDirectory );

            var solutionPath = this.GetFinalSolutionPath( context );
            var solutionDirectory = Path.GetDirectoryName( Path.GetFullPath( solutionPath ) );

            if ( solutionDirectory == null )
            {
                context.Console.WriteError( $"Unexpected format of solution file path '{solutionPath}'." );

                return false;
            }

            // Get the test.json file location relative to solution file based on full solution location path.
            var testJsonFile = Path.Combine( solutionDirectory, "test.json" );

            const string command = "test";
            var args = $"--logger \"trx\" --logger \"console;verbosity=minimal\" --results-directory {resultsDirectory}";

            if ( File.Exists( testJsonFile ) )
            {
                var testJsonFileContent = File.ReadAllText( testJsonFile );
                var testOptions = JsonConvert.DeserializeObject<TestOptions>( testJsonFileContent );

                if ( testOptions == null )
                {
                    context.Console.WriteError( $"No test options found in file '{testJsonFile}'." );

                    return false;
                }

                if ( !DotNetHelper.Run(
                        context,
                        settings,
                        solutionPath,
                        command,
                        args,
                        true,
                        out var exitCode,
                        out var output,
                        new ToolInvocationOptions( this.EnvironmentVariables ) ) )
                {
                    context.Console.WriteError( output );

                    return false;
                }

                if ( exitCode != 0 && !testOptions.IgnoreExitCode )
                {
                    return false;
                }

                if ( testOptions.ErrorRegexes != null )
                {
                    foreach ( var regex in testOptions.ErrorRegexes )
                    {
                        if ( Regex.IsMatch( output, regex, RegexOptions.IgnoreCase ) )
                        {
                            context.Console.WriteError( $"Output matched for pattern '{regex}'." );
                            context.Console.WriteError( output );

                            return false;
                        }
                    }
                }
            }
            else
            {
                if ( !this.RunDotNet( context, settings, command, args, true ) )
                {
                    return false;
                }
            }

            if ( TeamCityHelper.IsTeamCityBuild( settings ) )
            {
                // Export test result files to TeamCity.
                TeamCityHelper.SendImportDataMessage(
                    "vstest",
                    Path.Combine( resultsRelativeDirectory, "*.trx" ).Replace( Path.DirectorySeparatorChar, '/' ),
                    this.Name,
                    false );
            }

            return true;
        }

        public override bool Restore( BuildContext context, BuildSettings settings ) => this.RunDotNet( context, settings, "restore", "--no-cache", false );

        private string GetFinalSolutionPath( BuildContext context )
            => FileSystemHelper.GetFinalPath( Path.Combine( context.RepoDirectory, this.SolutionPath ) );

        private bool RunDotNet(
            BuildContext context,
            BuildSettings settings,
            string command,
            string arguments,
            bool addConfigurationFlag )
            => DotNetHelper.Run(
                context,
                settings,
                this.GetFinalSolutionPath( context ),
                command,
                arguments,
                addConfigurationFlag );
    }
}