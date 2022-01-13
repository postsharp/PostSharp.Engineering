using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using Spectre.Console;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies;

public abstract class ConfigureDependenciesCommand<T> : BaseCommand<T>
    where T : ConfigureDependenciesCommandSettings
{
    protected override bool ExecuteCore( BuildContext context, T settings )
    {
        context.Console.WriteHeading( "Setting the local dependencies" );

        if ( context.Product.Dependencies is { Length: > 0 } )
        {
            context.Console.WriteError( "This product has no dependency." );

            return false;
        }

        if ( settings.GetDependencies().Length == 0 && !settings.GetAllFlag() )
        {
            context.Console.WriteError( "No dependency was specified. Specify a dependency or use --all." );

            return false;
        }

        if ( !VersionsOverrideFile.TryLoad( context, out var versionsOverrideFile ) )
        {
            return false;
        }

        var dependencies = settings.GetAllFlag() ? context.Product.Dependencies.Select( x => x.Name ) : settings.GetDependencies();

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
                dependencyDefinition = context.Product.GetDependency( dependency );

                if ( dependencyDefinition == null )
                {
                    context.Console.WriteError( $"'{dependency}' is not a valid dependency name for this product. Use the 'dependencies list' command." );

                    return false;
                }
            }

            if ( !this.ConfigureDependency( context, versionsOverrideFile, dependencyDefinition, settings ) )
            {
                return false;
            }
        }

        // Fetching dependencies.
        context.Console.WriteImportantMessage( "Fetching dependencies" );

        if ( !FetchDependencyCommand.FetchDependenciesForAllConfigurations( context, versionsOverrideFile ) )
        {
            return false;
        }

        // Writing the version file.
        context.Console.WriteImportantMessage( $"Writing '{versionsOverrideFile.FilePath}'" );

        if ( !versionsOverrideFile.TrySave( context ) )
        {
            return false;
        }

        context.Console.Out.WriteLine();

        versionsOverrideFile.Print( context );

        context.Console.WriteSuccess( "Setting dependencies was successful." );

        return true;
    }

    protected abstract bool ConfigureDependency(
        BuildContext context,
        VersionsOverrideFile versionsOverrideFile,
        DependencyDefinition dependencyDefinition,
        T settings );
}