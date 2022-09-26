// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PostSharp.Engineering.BuildTools.Build.Solutions
{
    /// <summary>
    /// An implementation of <see cref="Solution"/> that uses the <c>msbuild</c> utility to build projects.
    /// </summary>
    public class MsbuildSolution : Solution
    {
        public MsbuildSolution( string solutionPath ) : base( solutionPath ) { }

        public override bool Build( BuildContext context, BuildSettings settings )
            => RunMsbuild( context, settings, this.SolutionPath, this.Name, "Build", "-p:RestorePackages=false" );

        public override bool Pack( BuildContext context, BuildSettings settings )
            => RunMsbuild( context, settings, this.SolutionPath, this.Name, "Pack", "-p:RestorePackages=false" );

        public override bool Test( BuildContext context, BuildSettings settings )
            => RunMsbuild( context, settings, this.SolutionPath, this.Name, "Test", "-p:RestorePackages=false" );

        public override bool Restore( BuildContext context, BuildSettings settings )
        {
            if ( Path.GetExtension( this.SolutionPath ) != ".sln" )
            {
                return RunMsbuild( context, settings, this.SolutionPath, this.Name, "Restore" );
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

                    if ( !RunMsbuild( context, settings, project, this.Name, "Restore" ) )
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private static bool RunMsbuild( BuildContext context, BuildSettings settings, string project, string solutionName, string target, string arguments = "" )
        {
            var argsBuilder = new StringBuilder();
            var path = Path.Combine( context.RepoDirectory, project );

            var binaryLogFilePath = Path.Combine(
                context.RepoDirectory,
                context.Product.LogsDirectory.ToString(),
                $"{solutionName}.{target}.binlog" );

            var configurationInfo = context.Product.Configurations[settings.BuildConfiguration];

            argsBuilder.Append(
                CultureInfo.InvariantCulture,
                $"-t:{target} -p:Configuration={configurationInfo.MSBuildName} \"{path}\" -v:{settings.Verbosity.ToAlias()} -NoLogo" );

            if ( settings.NoConcurrency )
            {
                argsBuilder.Append( " -m:1" );
            }

            argsBuilder.Append( CultureInfo.InvariantCulture, $" -bl:{binaryLogFilePath}" );

            foreach ( var property in settings.Properties )
            {
                argsBuilder.Append( CultureInfo.InvariantCulture, $" -p:{property.Key}={property.Value}" );
            }

            if ( !string.IsNullOrWhiteSpace( arguments ) )
            {
                argsBuilder.Append( " " + arguments.Trim() );
            }

            var toolInvocationOptions = new ToolInvocationOptions( DotNetHelper.GetMsBuildFixingEnvironmentVariables() );

            return ToolInvocationHelper.InvokeTool(
                context.Console,
                "msbuild",
                argsBuilder.ToString(),
                Environment.CurrentDirectory,
                toolInvocationOptions );
        }
    }
}