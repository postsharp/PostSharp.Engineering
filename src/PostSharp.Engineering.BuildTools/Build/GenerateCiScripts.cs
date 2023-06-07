// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Build;

public class GenerateCiScripts : BaseCommand<CommonCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, CommonCommandSettings settings )
    {
        return context.Product.GenerateTeamcityConfiguration( context, settings );
    }
}