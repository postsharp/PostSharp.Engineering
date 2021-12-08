using PostSharp.Engineering.BuildTools.Utilities;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class DotNetSolution : Solution
    {
        public DotNetSolution( string solutionPath ) : base( solutionPath ) { }

        public override bool Build( BuildContext context, BuildSettings settings ) => this.RunDotNet( context, settings, "build", "--no-restore" );

        public override bool Pack( BuildContext context, BuildSettings settings ) => this.RunDotNet( context, settings, "pack", "--no-restore" );

        public override bool Test( BuildContext context, BuildSettings settings ) => this.RunDotNet( context, settings, "test", "--no-restore" );

        public override bool Restore( BuildContext context, BaseBuildSettings settings ) => this.RunDotNet( context, settings, "restore", "--no-cache" );

        private bool RunDotNet( BuildContext context, BaseBuildSettings settings, string command, string arguments = "" )
            => DotNetHelper.Run(
                context,
                settings,
                Path.Combine( context.RepoDirectory, this.SolutionPath ),
                command,
                arguments );
    }
}