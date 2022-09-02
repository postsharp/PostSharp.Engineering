// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Build;

public class VerifyCommand : BaseCommand<PublishSettings>
{
    protected override bool ExecuteCore( BuildContext context, PublishSettings settings )
    {
        return context.Product.Verify( context, settings );
    }
}