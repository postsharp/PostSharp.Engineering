using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace PostSharp.Engineering.BuildTools.Utilities
{
    public static class DotNetHelper
    {
        public static bool Run(
            BuildContext context,
            BuildSettings settings,
            string solution,
            string command,
            string arguments = "" )
        {
            var argsBuilder = CreateCommandLine( context, settings, solution, command, arguments );

            return ToolInvocationHelper.InvokeTool(
                context.Console,
                "dotnet",
                argsBuilder,
                Environment.CurrentDirectory );
        }

        public static bool Run(
            BuildContext context,
            BuildSettings settings,
            string solution,
            string command,
            string arguments,
            out int exitCode,
            out string output )
        {
            var argsBuilder = CreateCommandLine( context, settings, solution, command, arguments );

            return ToolInvocationHelper.InvokeTool(
                context.Console,
                "dotnet",
                argsBuilder,
                Environment.CurrentDirectory,
                out exitCode,
                out output );
        }

        private static string CreateCommandLine( BuildContext context, BuildSettings settings, string solution, string command, string arguments )
        {
            var argsBuilder = new StringBuilder();
            var configuration = context.Product.Configurations[settings.BuildConfiguration];

            argsBuilder.Append(
                CultureInfo.InvariantCulture,
                $"{command} -p:Configuration={configuration.MSBuildName} \"{solution}\" -v:{settings.Verbosity.ToAlias()} --nologo" );

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

            return argsBuilder.ToString();
        }

        /// <summary>
        /// Returns the environment variables set by .NET Core with no values.
        /// Keeping the values set breaks MSBuild.
        /// We need to unset them to be able to execute MSBuild from a .NET Core process.
        /// </summary>
        public static ImmutableDictionary<string, string?> GetMsBuildFixingEnvironmentVariables()
        {
            var environmentVariablesBuilder = ImmutableDictionary.CreateBuilder<string, string?>();
            environmentVariablesBuilder.Add( "DOTNET_ROOT_X64", null );
            environmentVariablesBuilder.Add( "MSBUILD_EXE_PATH", null );
            environmentVariablesBuilder.Add( "MSBuildSDKsPath", null );

            return environmentVariablesBuilder.ToImmutable();
        }
    }
}