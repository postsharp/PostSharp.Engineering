// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using System;
using System.Globalization;
using System.Text;

namespace PostSharp.Engineering.BuildTools.Utilities
{
    [PublicAPI]
    public static class DotNetHelper
    {
        public static bool Run(
            BuildContext context,
            BuildSettings settings,
            string solution,
            string command,
            string arguments = "",
            bool addConfigurationFlag = false,
            ToolInvocationOptions? options = null )
        {
            var argsBuilder = CreateCommandLine( context, settings, solution, command, arguments, addConfigurationFlag );

            return ToolInvocationHelper.InvokeTool(
                context.Console,
                "dotnet",
                argsBuilder,
                Environment.CurrentDirectory,
                options );
        }

        public static bool Run(
            BuildContext context,
            BuildSettings settings,
            string solution,
            string command,
            string arguments,
            bool addConfigurationFlag,
            out int exitCode,
            out string output,
            ToolInvocationOptions? options = null )
        {
            var argsBuilder = CreateCommandLine( context, settings, solution, command, arguments, addConfigurationFlag );

            return ToolInvocationHelper.InvokeTool(
                context.Console,
                "dotnet",
                argsBuilder,
                Environment.CurrentDirectory,
                out exitCode,
                out output,
                options );
        }

        private static string CreateCommandLine(
            BuildContext context,
            BuildSettings settings,
            string solution,
            string command,
            string arguments,
            bool addConfigurationFlag )
        {
            var argsBuilder = new StringBuilder();

            argsBuilder.Append(
                CultureInfo.InvariantCulture,
                $"{command} \"{solution}\" -v:{settings.Verbosity.ToAlias()} --nologo" );

            if ( addConfigurationFlag )
            {
                var configuration = context.Product.Configurations[settings.BuildConfiguration];

                argsBuilder.Append( CultureInfo.InvariantCulture, $" -c {configuration.MSBuildName}" );
            }

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