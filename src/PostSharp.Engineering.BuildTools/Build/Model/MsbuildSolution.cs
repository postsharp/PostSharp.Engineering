using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class MsbuildSolution : Solution
    {
        public MsbuildSolution( string solutionPath ) : base( solutionPath ) { }

        public override bool Build( BuildContext context, BuildOptions options ) => this.RunMsbuild( context, options, "Build", "-p:RestorePackages=false" );

        public override bool Pack( BuildContext context, BuildOptions options ) => this.RunMsbuild( context, options, "Pack", "-p:RestorePackages=false" );

        public override bool Test( BuildContext context, BuildOptions options ) => this.RunMsbuild( context, options, "Test", "-p:RestorePackages=false" );

        public override bool Restore( BuildContext context, BaseBuildSettings options ) => this.RunMsbuild( context, options, "Restore" );

        private bool RunMsbuild( BuildContext context, BaseBuildSettings options, string target, string arguments = "" )
        {
            var argsBuilder = new StringBuilder();
            var path = Path.Combine( context.RepoDirectory, this.SolutionPath );
            argsBuilder.Append( CultureInfo.InvariantCulture, $"-t:{target} -p:Configuration={options.BuildConfiguration} \"{path}\" -v:{options.Verbosity.ToAlias()} -NoLogo" );

            if ( options.NoConcurrency )
            {
                argsBuilder.Append( " -m:1" );
            }

            foreach ( var property in options.Properties )
            {
                argsBuilder.Append( CultureInfo.InvariantCulture, $" -p:{property.Key}={property.Value}" );
            }

            if ( !string.IsNullOrWhiteSpace( arguments ) )
            {
                argsBuilder.Append( " " + arguments.Trim() );
            }

            return ToolInvocationHelper.InvokeTool(
                context.Console,
                "msbuild",
                argsBuilder.ToString(),
                Environment.CurrentDirectory );
        }
    }
}