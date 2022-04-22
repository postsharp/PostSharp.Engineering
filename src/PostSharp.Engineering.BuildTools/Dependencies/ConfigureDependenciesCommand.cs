using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using Spectre.Console;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies;

/// <summary>
/// Base class for <see cref="SetDependenciesCommand"/> and <see cref="ResetDependenciesCommand"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class ConfigureDependenciesCommand<T> : BaseCommand<T>
    where T : ConfigureDependenciesCommandSettings
{
    protected override bool ExecuteCore( BuildContext context, T settings )
    {
        // Validates the command line options.
        context.Console.WriteHeading( "Setting the local dependencies" );

        if ( context.Product.Dependencies is not { Length: > 0 } )
        {
            context.Console.WriteError( "This product has no dependency." );

            return false;
        }

        if ( settings.GetDependencies().Length == 0 && !settings.GetAllFlag() )
        {
            context.Console.WriteError( "No dependency was specified. Specify a dependency or use --all." );

            return false;
        }

        if ( !settings.TryGetBuildConfiguration( context, out var configuration ) )
        {
            return false;
        }

        // Loads the current version file.
        if ( !DependenciesOverrideFile.TryLoad( context, configuration, out var dependenciesOverrideFile ) )
        {
            return false;
        }

        // Iterate all matching dependencies.
        var dependencies = settings.GetAllFlag() ? context.Product.Dependencies.Select( x => x.Name ) : settings.GetDependencies();

        foreach ( var dependency in dependencies )
        {
            DependencyDefinition? dependencyDefinition;

            if ( int.TryParse( dependency, out var index ) )
            {
                // The dependency was given by position.

                if ( index < 1 || index > context.Product.Dependencies.Length )
                {
                    context.Console.WriteError( $"'{index}' is not a valid dependency index. Use the 'dependencies list' command." );

                    return false;
                }

                dependencyDefinition = context.Product.Dependencies[index - 1];
            }
            else
            {
                // The dependency was given by name.

                dependencyDefinition = context.Product.GetDependency( dependency );

                if ( dependencyDefinition == null )
                {
                    context.Console.WriteError( $"'{dependency}' is not a valid dependency name for this product. Use the 'dependencies list' command." );

                    return false;
                }
            }

            // Executes the logic itself.
            if ( !this.ConfigureDependency( context, dependenciesOverrideFile, dependencyDefinition, settings ) )
            {
                return false;
            }
        }

        // Fetching dependencies.
        context.Console.WriteImportantMessage( "Fetching dependencies" );

        if ( !FetchDependencyCommand.FetchDependencies( context, configuration, dependenciesOverrideFile ) )
        {
            return false;
        }

        // Writing the version file.
        context.Console.WriteImportantMessage( $"Writing '{dependenciesOverrideFile.FilePath}'" );

        if ( !dependenciesOverrideFile.TrySave( context ) )
        {
            return false;
        }

        context.Console.Out.WriteLine();

        dependenciesOverrideFile.Print( context );

        context.Console.WriteSuccess( "Setting dependencies was successful." );

        return true;
    }

    protected abstract bool ConfigureDependency(
        BuildContext context,
        DependenciesOverrideFile dependenciesOverrideFile,
        DependencyDefinition dependencyDefinition,
        T settings );
}