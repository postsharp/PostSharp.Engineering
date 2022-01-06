using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using Spectre.Console;
using System;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    public class ConfigureDependenciesCommand : BaseCommand<ConfigureDependenciesCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, ConfigureDependenciesCommandSettings settings )
        {
            context.Console.WriteHeading( "Setting the local dependencies" );

            if ( context.Product.Dependencies.IsDefaultOrEmpty )
            {
                context.Console.WriteError( "This product has no dependency." );

                return false;
            }

            if ( settings.Dependencies.Length == 0 && !settings.All )
            {
                context.Console.WriteError( "No dependency was specified. Specify a dependency or use --all." );

                return false;
            }

            var versionsOverrideFile = VersionsOverrideFile.Load( context );

            var dependencies = settings.All ? Model.Dependencies.All.Select( x => x.Name ) : settings.Dependencies;

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

                DependencySource dependencySource;

                if ( settings.Source != DependencySourceKind.BuildServer )
                {
                    dependencySource = DependencySource.CreateOfKind( settings.Source, "command" );
                }
                else if ( settings.BuildNumber != null )
                {
                    var ciBuildTypeId = settings.CiBuildTypeId ?? dependencyDefinition.DefaultCiBuildTypeId;
                    dependencySource = DependencySource.CreateBuildServerSource( settings.BuildNumber.Value, ciBuildTypeId, null, "command" );
                }
                else if ( settings.Branch != null )
                {
                    var branch = settings.Branch;
                    var ciBuildTypeId = settings.CiBuildTypeId ?? dependencyDefinition.DefaultCiBuildTypeId;
                    dependencySource = DependencySource.CreateBuildServerSource( branch, ciBuildTypeId, "command" );
                }
                else if ( settings.VersionDefiningDependencyName != null )
                {
                    dependencySource = DependencySource.CreateTransitiveBuildServerSource( settings.VersionDefiningDependencyName, null, "command" );
                }
                else
                {
                    var branch = dependencyDefinition.DefaultBranch;
                    var ciBuildTypeId = settings.CiBuildTypeId ?? dependencyDefinition.DefaultCiBuildTypeId;
                    dependencySource = DependencySource.CreateBuildServerSource( branch, ciBuildTypeId, "command" );
                }

                versionsOverrideFile.Dependencies[dependencyDefinition.Name] = dependencySource;
            }

            // Fetching dependencies.
            if ( settings.Source == DependencySourceKind.BuildServer )
            {
                // We need to fetch dependencies, otherwise we cannot find the version file.
                context.Console.WriteImportantMessage( "Fetching dependencies" );

                if ( !FetchDependencyCommand.FetchDependencies( context, versionsOverrideFile ) )
                {
                    return false;
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
    }
}