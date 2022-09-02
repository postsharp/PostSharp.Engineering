// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Cleans the current repo from build artefacts.
    /// </summary>
    public class CleanCommand : BaseCommand<BuildSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BuildSettings settings )
        {
            context.Product.Clean( context, settings );

            return true;
        }
    }
}