// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public class TeamCityBuildCommand : BaseCommand<TeamCityBuildCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, TeamCityBuildCommandSettings settings )
    {
        return TeamCityHelper.TriggerTeamCityBuild( context, settings, settings.TeamCityBuildType );
    }
}