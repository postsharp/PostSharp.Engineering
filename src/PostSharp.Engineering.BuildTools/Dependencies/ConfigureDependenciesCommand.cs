using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    public class ConfigureDependenciesCommand : BaseCommand<ConfigureDependenciesCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, ConfigureDependenciesCommandSettings options )
        {
            context.Console.WriteHeading( "Setting the local dependencies" );

            if ( context.Product.Dependencies.IsDefaultOrEmpty )
            {
                context.Console.WriteError( "This product has no dependency." );

                return false;
            }

            if ( options.Dependencies.Length == 0 && !options.All )
            {
                context.Console.WriteError( "No dependency was specified. Specify a dependency or use --all." );

                return false;
            }

            var versionsOverrideFile = VersionsOverrideFile.Load( context );

            var dependencies = options.All ? context.Product.Dependencies.Select( x => x.Name ) : options.Dependencies;

            foreach ( var dependency in dependencies )
            {
                DependencyDefinition? dependencyDefinition;

                if ( int.TryParse( dependency, out var index ) )
                {
                    if ( index < 1 || index > context.Product.Dependencies.Length )
                    {
                        context.Console.WriteError( $"'{index}' is not a valid dependency index. Use the 'dependencies list' command." );

                        return false;
                    }

                    dependencyDefinition = context.Product.Dependencies[index - 1];
                }
                else
                {
                    dependencyDefinition = context.Product.Dependencies.FirstOrDefault(
                        d =>
                            d.Name.Equals( dependency, StringComparison.OrdinalIgnoreCase ) );

                    if ( dependencyDefinition == null )
                    {
                        context.Console.WriteError( $"'{dependency}' is not a valid dependency name for this product. Use the 'dependencies list' command." );

                        return false;
                    }
                }

                var dependencySource = new DependencySource( options.Source, options.Branch ?? dependencyDefinition.DefaultBranch );
                versionsOverrideFile.Dependencies[dependencyDefinition.Name] = dependencySource;
            }

            // Fetching dependencies.
            if ( options.Source == DependencySourceKind.BuildServer )
            {
                // We need to fetch dependencies, otherwise we cannot find the version file.
                context.Console.WriteImportantMessage( "Fetching dependencies" );
                FetchDependencyCommand.FetchDependencies( context, versionsOverrideFile );

                foreach ( var dependency in versionsOverrideFile.Dependencies )
                {
                    if ( dependency.Value.SourceKind == DependencySourceKind.BuildServer )
                    {
                        // Find the version file.
                        var versionFile = FindVersionFile(
                            dependency.Key,
                            Path.Combine( context.RepoDirectory, context.Product.DependenciesDirectory, dependency.Key ) );

                        if ( versionFile == null )
                        {
                            context.Console.WriteError( $"Could not find {dependency.Key}.version.props." );

                            return false;
                        }

                        dependency.Value.VersionFile = versionFile;
                    }
                }
            }

            // Writing the version file.
            context.Console.WriteImportantMessage( $"Writing '{versionsOverrideFile.FilePath}'" );

            if ( !versionsOverrideFile.TrySave( context ) )
            {
                return false;
            }

            context.Console.Out.WriteLine();
            versionsOverrideFile.Print( context );

            context.Console.WriteSuccess( "Setting  dependencies was successful." );

            return true;
        }

        private static string? FindVersionFile( string productName, string directory )
        {
            var path = Path.Combine( directory, productName + ".version.props" );

            if ( File.Exists( path ) )
            {
                return path;
            }

            foreach ( var subdirectory in Directory.GetDirectories( directory ) )
            {
                path = FindVersionFile( productName, subdirectory );

                if ( path != null )
                {
                    return path;
                }
            }

            return null;
        }
    }
}