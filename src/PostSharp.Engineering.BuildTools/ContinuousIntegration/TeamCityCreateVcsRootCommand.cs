// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public class TeamCityCreateVcsRootCommand : BaseCommand<TeamCityCreateVcsRootCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, TeamCityCreateVcsRootCommandSettings settings )
    {
        context.Console.WriteHeading( $"Creating VCS root" );
        
        if ( !TeamCityHelper.TryConnectTeamCity( context, out var tc ) )
        {
            return false;
        }

        if ( !tc.TryCreateVcsRoot( context.Console, settings.Url, settings.ProjectId, settings.Version, out var name, out var id ) )
        {
            return false;
        }
        
        context.Console.WriteSuccess( $"VCS root created. Name: \"{name}\" ID: \"{id}\"" );

        return true;
    }
}