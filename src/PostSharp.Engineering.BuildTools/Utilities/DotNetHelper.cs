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
            BaseBuildSettings settings,
            string solution,
            string command,
            string arguments = "" )
        {
            var argsBuilder = new StringBuilder();
            var configuration = context.Product.Configurations[settings.BuildConfiguration];

            argsBuilder.Append(
                CultureInfo.InvariantCulture,
                $"{command} -p:Configuration={configuration.MSBuildName} -p:EngineeringConfiguration={settings.BuildConfiguration} \"{solution}\" -v:{settings.Verbosity.ToAlias()} --nologo" );

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
                "dotnet",
                argsBuilder.ToString(),
                Environment.CurrentDirectory );
        }
    }
}