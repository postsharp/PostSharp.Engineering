// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Build;

public class BumpCommand : BaseCommand<BumpSettings>
{
    protected override bool ExecuteCore( BuildContext context, BumpSettings settings )
    {
        return context.Product.BumpVersion( context, settings );
    }
}