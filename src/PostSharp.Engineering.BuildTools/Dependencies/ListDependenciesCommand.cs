using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    /// <summary>
    /// Lists dependencies.
    /// </summary>
    public class ListDependenciesCommand : BaseCommand<BaseDependenciesCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BaseDependenciesCommandSettings settings )
        {
            var productDependencies = context.Product.Dependencies;

            if ( productDependencies is not { Length: > 0 } )
            {
                context.Console.WriteImportantMessage( $"{context.Product.ProductName} has no dependency." );
            }
            else
            {
                context.Console.WriteImportantMessage( $"{context.Product.ProductName} has {productDependencies.Length} explicit dependencies:" );

                if ( !settings.TryGetBuildConfiguration( context, out var configuration ) )
                {
                    return false;
                }

                if ( !VersionsOverrideFile.TryLoad( context, configuration, out var versionsOverrideFile ) )
                {
                    return false;
                }

                versionsOverrideFile.Print( context );
            }

            return true;
        }
    }
}