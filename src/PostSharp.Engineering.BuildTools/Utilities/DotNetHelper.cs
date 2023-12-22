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

            options = AddSimulatedContinuousIntegrationEnvironmentVariables( settings, options );

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
            
            options = AddSimulatedContinuousIntegrationEnvironmentVariables( settings, options );

            return ToolInvocationHelper.InvokeTool(
                context.Console,
                "dotnet",
                argsBuilder,
                Environment.CurrentDirectory,
                out exitCode,
                out output,
                options );
        }

        private static ToolInvocationOptions? AddSimulatedContinuousIntegrationEnvironmentVariables( BuildSettings settings, ToolInvocationOptions? options )
        {
            var environmentVariables = TeamCityHelper.GetSimulatedContinuousIntegrationEnvironmentVariables( settings );

            if ( environmentVariables.IsEmpty )
            {
                return options;
            }
            else if ( options == null )
            {
                return new ToolInvocationOptions( environmentVariables );
            }
            else
            {
                return options.WithEnvironmentVariables( environmentVariables );
            }
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

            var isRunCommand = command == "run";

            var projectPrefix = string.Empty;
            var nologo = " --nologo";

            if ( isRunCommand )
            {
                // The command `dotnet run SomeProject.csproj` does not work, it requires explicit argument name.
                projectPrefix = "--project ";
                // dotnet run does not support --nologo.
                nologo = string.Empty;
            }

            argsBuilder.Append(
                CultureInfo.InvariantCulture,
                $"{command} {projectPrefix}\"{projectOrSolution}\" -v:{settings.Verbosity.ToAlias()}{nologo}" );

            if ( addConfigurationFlag )
            {
                argsBuilder.Append(
                    CultureInfo.InvariantCulture,
                    $" -c {context.Product.DependencyDefinition.MSBuildConfiguration[settings.BuildConfiguration]}" );
            }

            if ( settings.NoConcurrency )
            {
                argsBuilder.Append( " -m:1" );
            }

            foreach ( var property in settings.Properties )
            {
                argsBuilder.Append( CultureInfo.InvariantCulture, $" -p:{property.Key}={property.Value}" );
            }

            if ( TeamCityHelper.IsTeamCityBuild( settings ) )
            {
                argsBuilder.Append( " -p:ContinuousIntegrationBuild=True" );
            }

            if ( !isRunCommand )
            {
                var binaryLogFilePath = Path.Combine(
                    context.RepoDirectory,
                    context.Product.LogsDirectory.ToString(),
                    $"{Path.GetFileName( projectOrSolution )}.{command}.binlog" );

                argsBuilder.Append( CultureInfo.InvariantCulture, $" -bl:\"{binaryLogFilePath}\"" );
            }

            if ( !string.IsNullOrWhiteSpace( arguments ) )
            {
                argsBuilder.Append( " " + arguments.Trim() );
            }

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
                context.Product.TestResultsDirectory.ToString( new BuildInfo( null, settings.BuildConfiguration, context.Product, null ) );

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

            if ( !string.IsNullOrEmpty( settings.TestsFilter ) )
            {
                args += $" --filter \"{settings.TestsFilter}\"";
            }

            if ( !string.IsNullOrWhiteSpace( additionalArguments ) )
            {
                args += $" {additionalArguments}";
            }

            bool success;

            if ( File.Exists( testJsonFile ) )
            {
                var testJsonFileContent = File.ReadAllText( testJsonFile );
                var testOptions = JsonConvert.DeserializeObject<TestOptions>( testJsonFileContent );

                if ( testOptions == null )
                {
                    context.Console.WriteError( $"No test options found in file '{testJsonFile}'." );

                    return false;
                }

                _ = Run(
                    context,
                    settings,
                    projectOrSolution,
                    command,
                    args,
                    true,
                    out var exitCode,
                    out var output,
                    new ToolInvocationOptions( environmentVariables ).WithEnvironmentVariables(
                        TeamCityHelper.GetSimulatedContinuousIntegrationEnvironmentVariables( settings ) ) );

                success = exitCode != 0 && !testOptions.IgnoreExitCode;

                if ( exitCode != 0 )
                {
                    context.Console.WriteError( output );
                }
                else
                {
                    if ( testOptions.ErrorRegexes != null )
                    {
                        foreach ( var regex in testOptions.ErrorRegexes )
                        {
                            if ( Regex.IsMatch( output, regex, RegexOptions.IgnoreCase ) )
                            {
                                context.Console.WriteError( $"Output matched for pattern '{regex}'." );
                                context.Console.WriteError( output );

                                success = false;
                            }
                        }
                    }

                    if ( success )
                    {
                        context.Console.WriteMessage( output );
                    }
                }
            }
            else
            {
                success = Run(
                    context,
                    settings,
                    projectOrSolution,
                    command,
                    args,
                    true,
                    new ToolInvocationOptions( TeamCityHelper.GetSimulatedContinuousIntegrationEnvironmentVariables( settings ) ) );
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

            return success;
        }
    }
}