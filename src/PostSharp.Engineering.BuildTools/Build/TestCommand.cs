// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Executes the tests.
    /// </summary>
    public class TestCommand : BaseCommand<BuildSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BuildSettings settings )
        {
            return context.Product.Test( context, settings );
        }
    }
}