using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    /// <summary>
    /// Lists dependencies.
    /// </summary>
    public class ListDependenciesCommand : BaseCommand<CommonCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, CommonCommandSettings settings )
        {
            var productDependencies = context.Product.Dependencies;

            if ( productDependencies is not { Length: > 0 } )
            {
                context.Console.WriteImportantMessage( $"{context.Product.ProductName} has no dependency." );
            }
            else
            {
                context.Console.WriteImportantMessage( $"{context.Product.ProductName} has {productDependencies.Length} explicit dependencies:" );

                if ( !VersionsOverrideFile.TryLoad( context, out var versionsOverrideFile ) )
                {
                    return false;
                }

                versionsOverrideFile.Print( context );
            }

            return true;
        }
    }
}