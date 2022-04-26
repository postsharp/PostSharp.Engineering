using PostSharp.Engineering.BuildTools.Utilities;

namespace PostSharp.Engineering.BuildTools.Build;

public class TeamCityBumpCommand : BaseCommand<TeamCityCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, TeamCityCommandSettings settings )
    {
        return TeamCityHelper.TriggerTeamCityVersionBump( context, settings, out _ );
    }
}