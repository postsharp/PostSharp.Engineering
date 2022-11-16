// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using System;
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
                $"{command} \"{solution}\" -p:Configuration={configuration.MSBuildName} -v:{settings.Verbosity.ToAlias()} --nologo" );

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
    }
}