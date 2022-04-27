using PostSharp.Engineering.BuildTools.ContinuousIntegration;

namespace PostSharp.Engineering.BuildTools.Build;

public class TeamCityBumpCommand : BaseCommand<TeamCityCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, TeamCityCommandSettings settings )
    {
        return TeamCityHelper.TriggerTeamCityVersionBump( context, settings );
    }
}