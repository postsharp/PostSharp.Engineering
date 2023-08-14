// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;

namespace PostSharp.Engineering.BuildTools.Dependencies;

public class UpdateAutoUpdatedDependenciesCommand : BaseCommand<PublishSettings>
{
    protected override bool ExecuteCore( BuildContext context, PublishSettings settings )
    {
        context.Console.WriteHeading( "Updating auto-updated dependencies" );

        if ( !AutoUpdatedDependenciesHelper.TryParseAndVerifyDependencies( context, settings, out var dependenciesUpdated ) )
        {
            return false;
        }

        context.Console.WriteSuccess( dependenciesUpdated ? "Auto-updated dependencies updated." : "Auto-updated dependencies were up to date already." );

        return true;
    }
}