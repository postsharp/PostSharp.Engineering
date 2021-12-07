using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using Spectre.Console;
using System.IO;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    public class ListDependenciesCommand : BaseCommand<BaseCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BaseCommandSettings options )
        {
            var productDependencies = context.Product.Dependencies;

            if ( productDependencies.IsDefaultOrEmpty )
            {
                context.Console.WriteImportantMessage( $"{context.Product.ProductName} has no dependency." );
            }
            else
            {
                context.Console.WriteImportantMessage( $"{context.Product.ProductName} has {productDependencies.Length} dependencies:" );

                var versionsOverrideFile = VersionsOverrideFile.Load( context );

                versionsOverrideFile.Print( context );
            }

            return true;
        }
    }
}