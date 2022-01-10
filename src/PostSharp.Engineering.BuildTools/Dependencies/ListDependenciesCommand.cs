using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    public class ListDependenciesCommand : BaseCommand<BaseCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BaseCommandSettings settings )
        {
            var productDependencies = context.Product.Dependencies;

            if ( productDependencies.IsDefaultOrEmpty )
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