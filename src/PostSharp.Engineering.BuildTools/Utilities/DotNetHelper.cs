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
                context.GetWorkingDirectory( projectOrSolution ),
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
                context.GetWorkingDirectory( projectOrSolution ),
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
                return options.AddEnvironmentVariables( environmentVariables );
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

            if ( settings.DisableBuildServers )
            {
                argsBuilder.Append( " --disable-build-servers" );
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
            string? additionalArguments = null,
            bool buildFirst = false )
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

            var options = new ToolInvocationOptions( environmentVariables ).AddEnvironmentVariables(
                TeamCityHelper.GetSimulatedContinuousIntegrationEnvironmentVariables( settings ) );

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

                string output;

                string? buildOutput = null;
                var buildSuccess = true;

                if ( buildFirst )
                {
                    _ = Run(
                        context,
                        settings,
                        projectOrSolution,
                        "build",
                        "",
                        true,
                        out var buildExitCode,
                        out buildOutput,
                        options );

                    buildSuccess = buildExitCode == 0;
                }

                if ( !buildSuccess )
                {
                    success = testOptions.IgnoreExitCode;
                    output = buildOutput!;
                }
                else
                {
                    _ = Run(
                        context,
                        settings,
                        projectOrSolution,
                        command,
                        args,
                        true,
                        out var testExitCode,
                        out var testOutput,
                        options );

                    success = testExitCode == 0 || testOptions.IgnoreExitCode;
                    output = buildOutput == null ? testOutput : buildOutput + Environment.NewLine + testOutput;
                }

                if ( success )
                {
                    if ( testOptions.ErrorRegexes != null )
                    {
                        foreach ( var regex in testOptions.ErrorRegexes )
                        {
                            if ( Regex.IsMatch( output, regex, RegexOptions.IgnoreCase ) )
                            {
                                context.Console.WriteError( $"Output matched for pattern '{regex}'." );

                                success = false;
                            }
                        }
                    }

                    if ( testOptions.SuccessRegexes != null )
                    {
                        foreach ( var regex in testOptions.SuccessRegexes )
                        {
                            if ( !Regex.IsMatch( output, regex, RegexOptions.IgnoreCase ) )
                            {
                                context.Console.WriteError( $"Output did not match for pattern '{regex}'." );

                                success = false;
                            }
                        }
                    }
                }

                if ( success )
                {
                    context.Console.WriteMessage( output );
                }
                else
                {
                    context.Console.WriteError( output );
                }
            }
            else
            {
                if ( buildFirst )
                {
                    success = Run(
                        context,
                        settings,
                        projectOrSolution,
                        "build",
                        "",
                        true,
                        options );
                }
                else
                {
                    success = true;
                }

                if ( success )
                {
                    success = Run(
                        context,
                        settings,
                        projectOrSolution,
                        command,
                        args,
                        true,
                        options );
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

            return success;
        }
    }
}