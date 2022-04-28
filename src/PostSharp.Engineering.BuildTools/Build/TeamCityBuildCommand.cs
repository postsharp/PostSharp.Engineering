using PostSharp.Engineering.BuildTools.ContinuousIntegration;

namespace PostSharp.Engineering.BuildTools.Build;

public class TeamCityBuildCommand : BaseCommand<TeamCityCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, TeamCityCommandSettings settings )
    {
        return TeamCityHelper.TriggerTeamCityBuild( context, settings, settings.BuildType );
    }
}