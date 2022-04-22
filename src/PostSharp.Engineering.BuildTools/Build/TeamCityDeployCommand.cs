using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build;

public class TeamCityDeployCommand : BaseCommand<TeamCityCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, TeamCityCommandSettings settings )
    {
        return TeamCityHelper.TriggerTeamCityDeploy( context, settings );
    }
}