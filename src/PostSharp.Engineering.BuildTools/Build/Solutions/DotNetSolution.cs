using Newtonsoft.Json;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System.Collections.Generic;
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

        public override bool Build( BuildContext context, BuildSettings settings ) => this.RunDotNet( context, settings, "build", "--no-restore" );

        public override bool Pack( BuildContext context, BuildSettings settings ) => this.RunDotNet( context, settings, "pack", "--no-restore" );

        public override bool Test( BuildContext context, BuildSettings settings )
        {
            var resultsDirectory = context.Product.TestResultsDirectory.ToString( new BuildInfo( null!, settings.BuildConfiguration, context.Product ) );

            return this.RunDotNet(
                context,
                settings,
                "test",
                $"--no-restore --logger \"trx\" --logger \"console;verbosity=minimal\" --results-directory {resultsDirectory}" );
        }

        public override bool Restore( BuildContext context, BuildSettings settings ) => this.RunDotNet( context, settings, "restore", "--no-cache" );

        private bool RunDotNet( BuildContext context, BuildSettings settings, string command, string arguments = "" )
        {
            var allArguments = new List<string>() { arguments };

            var binaryLogFilePath = Path.Combine(
                context.RepoDirectory,
                context.Product.LogsDirectory.ToString(),
                $"{context.Product.ProductName}.{command}.binlog" );

            if ( settings.ContinuousIntegration )
            {
                allArguments.Add( "-p:ContinuousIntegrationBuild=True" );
            }

            allArguments.Add( $"-bl:{binaryLogFilePath}" );

            // Get the test.json file location relative to solution file based on full solution location path.
            var testJsonFile = Path.Combine(
                Path.GetDirectoryName( Path.GetFullPath( this.SolutionPath ) )
                ?? Path.Combine( context.RepoDirectory, this.SolutionPath ),
                "test.json" );

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
                        Path.Combine( context.RepoDirectory, this.SolutionPath ),
                        command,
                        string.Join( " ", allArguments ),
                        out var exitCode,
                        out var output ) )
                {
                    context.Console.WriteError( output );

                    return false;
                }

                if ( exitCode != 0 && !testOptions.IgnoreExitCode )
                {
                    return false;
                }

                if ( testOptions.OutputRegexes != null )
                {
                    foreach ( var regex in testOptions.OutputRegexes )
                    {
                        if ( Regex.IsMatch( output, regex, RegexOptions.IgnoreCase ) )
                        {
                            context.Console.WriteError( $"Output matched for pattern '{regex}'." );
                            context.Console.WriteError( output );

                            return false;
                        }
                    }
                }

                return true;
            }
            else
            {
                return DotNetHelper.Run(
                    context,
                    settings,
                    Path.Combine( context.RepoDirectory, this.SolutionPath ),
                    command,
                    string.Join( " ", allArguments ) );
            }
        }
    }
}