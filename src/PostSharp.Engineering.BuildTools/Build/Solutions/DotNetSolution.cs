// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System.IO;

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
            => DotNetHelper.RunTests( context, settings, this.GetFinalSolutionPath( context ), this.EnvironmentVariables );

        public bool BuildAndTest( BuildContext context, BuildSettings settings )
            => DotNetHelper.RunTests( context, settings, this.GetFinalSolutionPath( context ), this.EnvironmentVariables, buildFirst: true );

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