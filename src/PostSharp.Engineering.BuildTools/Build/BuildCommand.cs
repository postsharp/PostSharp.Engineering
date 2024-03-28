// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;

namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Builds the product.
    /// </summary>
    [UsedImplicitly]
    public class BuildCommand : BaseCommand<BuildSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BuildSettings settings )
        {
            if ( context.Product.TestOnBuild )
            {
                context.Console.WriteWarning( "'test' command executed instead." );
                
                return context.Product.Test( context, settings );    
            }
            else
            {
                return context.Product.Build( context, settings );
            }
        }
    }
}