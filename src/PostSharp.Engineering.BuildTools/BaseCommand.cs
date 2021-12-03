using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

#pragma warning disable 8765

namespace PostSharp.Engineering.BuildTools
{
    public abstract class BaseCommand<T> : Command<T>
        where T : BaseCommandSettings
    {
        public sealed override int Execute( CommandContext context, T settings )
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                if ( !BuildContext.TryCreate( context, out var buildContext ) )
                {
                    return 1;
                }
                else
                {
                    // Validate custom properties.
                    if ( settings.ListProperties )
                    {
                        if ( buildContext.Product.SupportedProperties.Count > 0 )
                        {
                            buildContext.Console.WriteImportantMessage( "The following properties are supported by this product:" );

                            foreach ( var property in buildContext.Product.SupportedProperties )
                            {
                                buildContext.Console.WriteImportantMessage( $"\t{property.Key}: {property.Value}" );
                            }
                        }
                        else
                        {
                            buildContext.Console.WriteImportantMessage( "The current product does not support any property." );
                        }

                        return 1;
                    }

                    var unsupportedProperties =
                        settings.Properties.Keys
                            .Where( name => !buildContext.Product.SupportedProperties.ContainsKey( name ) )
                            .ToList();

                    if ( unsupportedProperties.Count > 0 )
                    {
                        buildContext.Console.WriteError(
                            $"The following properties are not supported: {string.Join( ", ", unsupportedProperties )}. Use --list-properties to list the supported properties." );

                        return 1;
                    }

                    // Display the logo.
                    buildContext.Console.Out.Write(
                        new FigletText( buildContext.Product.ProductName )
                            .LeftAligned()
                            .Color( Color.Purple ) );

                    buildContext.Console.Out.WriteLine();
                    var myVersion = GetMyVersion();
                    buildContext.Console.WriteMessage( $"Using PostSharp.Engineering v{myVersion}." );
                    buildContext.Console.Out.WriteLine();

                    // Validate the sdk version in global.sdk.
                    if ( buildContext.Product.RequiresEngineeringSdk )
                    {
                        var globalJsonPath = Path.Combine( buildContext.RepoDirectory, "global.json" );

                        if ( !File.Exists( globalJsonPath ) )
                        {
                            buildContext.Console.WriteWarning( "global.json does not exist." );
                        }

                        var globalJson = JsonDocument.Parse( File.ReadAllText( globalJsonPath ) );

                        if ( !globalJson.RootElement.TryGetProperty( "msbuild-sdks", out var sdks ) ||
                             !sdks.TryGetProperty( "PostSharp.Engineering.Sdk", out var sdk ) ||
                             sdk.GetString() == null )
                        {
                            buildContext.Console.WriteWarning( "global.json does not import the PostSharp.Engineering.Sdk." );
                        }
                        else
                        {
                            if ( sdk.GetString() != myVersion )
                            {
                                buildContext.Console.WriteWarning(
                                    $"global.json imports PostSharp.Engineering.Sdk version {sdk.GetString()}, but the BuildTools version is {myVersion}." );
                            }
                        }
                    }

                    // Execute the command itself.
                    var success = this.ExecuteCore( buildContext, settings );

                    buildContext.Console.WriteMessage( $"Finished at {DateTime.Now} after {stopwatch.Elapsed}." );

                    return success ? 0 : 1;
                }
            }
            catch ( Exception ex )
            {
                AnsiConsole.WriteException( ex );

                return 10;
            }
        }

        protected abstract bool ExecuteCore( BuildContext context, T options );

        protected static bool CheckNoChange( BuildContext context, BaseCommandSettings options, string repo )
        {
            if ( !options.Force )
            {
                if ( !ToolInvocationHelper.InvokeTool(
                         context.Console,
                         "git",
                         $"status --porcelain",
                         repo,
                         out var exitCode,
                         out var statusOutput )
                     || exitCode != 0 )
                {
                    return false;
                }

                if ( !string.IsNullOrWhiteSpace( statusOutput ) )
                {
                    context.Console.WriteError( $"There are non-committed changes in '{repo}' Use --force." );
                    context.Console.WriteImportantMessage( statusOutput );

                    return false;
                }
            }

            return true;
        }

        private static string? GetMyVersion()
            => typeof(BaseCommand<>).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault( a => a.Key == "PackageVersion" )?.Value;
    }
}