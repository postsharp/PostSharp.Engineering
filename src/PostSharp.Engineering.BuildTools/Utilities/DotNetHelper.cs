// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using Newtonsoft.Json;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PostSharp.Engineering.BuildTools.Utilities
{
    [PublicAPI]
    public static class DotNetHelper
    {
        public static bool Run(
            BuildContext context,
            BuildSettings settings,
            string projectOrSolution,
            string command,
            string arguments = "",
            bool addConfigurationFlag = false,
            ToolInvocationOptions? options = null )
        {
            var argsBuilder = CreateCommandLine( context, settings, projectOrSolution, command, arguments, addConfigurationFlag );

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
            string projectOrSolution,
            string command,
            string arguments,
            bool addConfigurationFlag,
            out int exitCode,
            out string output,
            ToolInvocationOptions? options = null )
        {
            var argsBuilder = CreateCommandLine( context, settings, projectOrSolution, command, arguments, addConfigurationFlag );

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
            string projectOrSolution,
            string command,
            string arguments,
            bool addConfigurationFlag )
        {
            var argsBuilder = new StringBuilder();

            argsBuilder.Append(
                CultureInfo.InvariantCulture,
                $"{command} \"{projectOrSolution}\" -v:{settings.Verbosity.ToAlias()} --nologo" );

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

            if ( TeamCityHelper.IsTeamCityBuild( settings ) )
            {
                argsBuilder.Append( " -p:ContinuousIntegrationBuild=True" );
            }

            var binaryLogFilePath = Path.Combine(
                context.RepoDirectory,
                context.Product.LogsDirectory.ToString(),
                $"{Path.GetFileName( projectOrSolution )}.{command}.binlog" );

            argsBuilder.Append( CultureInfo.InvariantCulture, $" -bl:\"{binaryLogFilePath}\"" );

            return argsBuilder.ToString();
        }

        public static bool RunTests(
            BuildContext context,
            BuildSettings settings,
            string projectOrSolution,
            ImmutableDictionary<string, string?>? environmentVariables = null,
            string? additionalArguments = null )
        {
            var resultsRelativeDirectory =
                context.Product.TestResultsDirectory.ToString( new BuildInfo( null!, settings.BuildConfiguration, context.Product ) );

            var resultsDirectory = Path.Combine( context.RepoDirectory, resultsRelativeDirectory );

            var projectOrSolutionDirectory = Path.GetDirectoryName( Path.GetFullPath( projectOrSolution ) );

            if ( projectOrSolutionDirectory == null )
            {
                context.Console.WriteError( $"Unexpected format of project or solution file path '{projectOrSolution}'." );

                return false;
            }

            // Get the test.json file location relative to solution file based on full solution location path.
            var testJsonFile = Path.Combine( projectOrSolutionDirectory, "test.json" );

            const string command = "test";
            var args = $"--logger \"trx\" --logger \"console;verbosity=minimal\" --results-directory \"{resultsDirectory}\"";

            if ( !string.IsNullOrWhiteSpace( additionalArguments ) )
            {
                args += $" {additionalArguments}";
            }

            if ( File.Exists( testJsonFile ) )
            {
                var testJsonFileContent = File.ReadAllText( testJsonFile );
                var testOptions = JsonConvert.DeserializeObject<TestOptions>( testJsonFileContent );

                if ( testOptions == null )
                {
                    context.Console.WriteError( $"No test options found in file '{testJsonFile}'." );

                    return false;
                }

                if ( !Run(
                        context,
                        settings,
                        projectOrSolution,
                        command,
                        args,
                        true,
                        out var exitCode,
                        out var output,
                        new ToolInvocationOptions( environmentVariables ) ) )
                {
                    context.Console.WriteError( output );

                    return false;
                }

                if ( exitCode != 0 && !testOptions.IgnoreExitCode )
                {
                    return false;
                }

                if ( testOptions.ErrorRegexes != null )
                {
                    foreach ( var regex in testOptions.ErrorRegexes )
                    {
                        if ( Regex.IsMatch( output, regex, RegexOptions.IgnoreCase ) )
                        {
                            context.Console.WriteError( $"Output matched for pattern '{regex}'." );
                            context.Console.WriteError( output );

                            return false;
                        }
                    }
                }
            }
            else
            {
                if ( !Run(
                        context,
                        settings,
                        projectOrSolution,
                        command,
                        args,
                        true ) )
                {
                    return false;
                }
            }

            if ( TeamCityHelper.IsTeamCityBuild( settings ) )
            {
                // Export test result files to TeamCity.
                TeamCityHelper.SendImportDataMessage(
                    "vstest",
                    Path.Combine( resultsRelativeDirectory, "*.trx" ).Replace( Path.DirectorySeparatorChar, '/' ),
                    Path.GetFileName( projectOrSolution ),
                    false );
            }

            return true;
        }
    }
}