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

        public override bool Build( BuildContext context, BuildSettings settings ) => this.RunMsbuild( context, settings, "Build", "-p:RestorePackages=false" );

        public override bool Pack( BuildContext context, BuildSettings settings ) => this.RunMsbuild( context, settings, "Pack", "-p:RestorePackages=false" );

        public override bool Test( BuildContext context, BuildSettings settings ) => this.RunMsbuild( context, settings, "Test", "-p:RestorePackages=false" );

        public override bool Restore( BuildContext context, BaseBuildSettings settings ) => this.RunMsbuild( context, settings, "Restore" );

        private bool RunMsbuild( BuildContext context, BaseBuildSettings settings, string target, string arguments = "" )
        {
            var argsBuilder = new StringBuilder();
            var path = Path.Combine( context.RepoDirectory, this.SolutionPath );
            argsBuilder.Append( CultureInfo.InvariantCulture, $"-t:{target} -p:Configuration={settings.BuildConfiguration} \"{path}\" -v:{settings.Verbosity.ToAlias()} -NoLogo" );

            if ( settings.NoConcurrency )
            {
                argsBuilder.Append( " -m:1" );
            }

            foreach ( var property in settings.Properties )
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