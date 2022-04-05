using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            
            return this.RunDotNet( context, settings, "test", $"--no-restore --logger \"trx\" --logger \"console;verbosity=minimal\" --results-directory {resultsDirectory}" );
        }

        public override bool Restore( BuildContext context, BuildSettings settings ) => this.RunDotNet( context, settings, "restore", "--no-cache" );

        private bool RunDotNet( BuildContext context, BuildSettings settings, string command, string arguments = "" )
        {
            var allArguments = new List<string>() { arguments };

            if ( settings.ContinuousIntegration )
            {
                allArguments.Add( "-p:ContinuousIntegrationBuild=True" );
            }
            
            return DotNetHelper.Run(
                context,
                settings,
                Path.Combine( context.RepoDirectory, this.SolutionPath ),
                command,
                string.Join( " ", allArguments ) );
        }
    }
}