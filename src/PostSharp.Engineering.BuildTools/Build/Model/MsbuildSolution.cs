using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class MsbuildSolution : Solution
    {
        public MsbuildSolution( string solutionPath ) : base( solutionPath ) { }

        public override bool Build( BuildContext context, BuildSettings settings )
            => this.RunMsbuild( context, settings, this.SolutionPath, "Build", "-p:RestorePackages=false" );

        public override bool Pack( BuildContext context, BuildSettings settings )
            => this.RunMsbuild( context, settings, this.SolutionPath, "Pack", "-p:RestorePackages=false" );

        public override bool Test( BuildContext context, BuildSettings settings )
            => this.RunMsbuild( context, settings, this.SolutionPath, "Test", "-p:RestorePackages=false" );

        public override bool Restore( BuildContext context, BaseBuildSettings settings )
        {
            if ( Path.GetExtension( this.SolutionPath ) != ".sln" )
            {
                return this.RunMsbuild( context, settings, this.SolutionPath, "Restore" );
            }
            else
            {
                // "msbuild -t Restore" doesn't call the Restore target for projects referencing NuGet packages using packages.config in a solution.
                // We call the Restore target ourselves on each project in the solution.

                var exe = "dotnet";
                var args = $"sln \"{this.SolutionPath}\" list";

                if ( !ToolInvocationHelper.InvokeTool( context.Console, exe, args, context.RepoDirectory, out var _, out var slnListOutput ) )
                {
                    context.Console.WriteError( $"Error executing {exe} {args}" );
                    context.Console.WriteError( slnListOutput );

                    return false;
                }

                // The "dotnet sln list" command output contains a header, so we need to filter the rows.
                var projectList = slnListOutput.Split( '\r', '\n' ).Where( l => l.EndsWith( "proj", StringComparison.OrdinalIgnoreCase ) ).ToArray();

                if ( projectList.Length == 0 )
                {
                    throw new InvalidOperationException();
                }

                foreach ( var project in projectList )
                {
                    context.Console.WriteMessage( $"Restoring {project}" );

                    if ( !this.RunMsbuild( context, settings, project, "Restore" ) )
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private bool RunMsbuild( BuildContext context, BaseBuildSettings settings, string project, string target, string arguments = "" )
        {
            var argsBuilder = new StringBuilder();
            var path = Path.Combine( context.RepoDirectory, project );

            var configuration = context.Product.Configurations[settings.BuildConfiguration];

            argsBuilder.Append(
                CultureInfo.InvariantCulture,
                $"-t:{target} -p:Configuration={configuration.MSBuildName} -p:EngineeringConfiguration={settings.BuildConfiguration} \"{path}\" -v:{settings.Verbosity.ToAlias()} -NoLogo" );

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

            // The following environment variables are set by .NET Core and break MSBuild.
            // We need to unset them to be able to execute MSBuild from a .NET Core process.
            var environmentVariablesBuilder = ImmutableDictionary.CreateBuilder<string, string?>();
            environmentVariablesBuilder.Add( "DOTNET_ROOT_X64", null );
            environmentVariablesBuilder.Add( "MSBUILD_EXE_PATH", null );
            environmentVariablesBuilder.Add( "MSBuildSDKsPath", null );
            environmentVariablesBuilder.Add( "MSBuildSDKsPath", null );

            var toolInvokationOptions = new ToolInvocationOptions( environmentVariablesBuilder.ToImmutable() );

            return ToolInvocationHelper.InvokeTool(
                context.Console,
                "msbuild",
                argsBuilder.ToString(),
                Environment.CurrentDirectory,
                toolInvokationOptions );
        }
    }
}