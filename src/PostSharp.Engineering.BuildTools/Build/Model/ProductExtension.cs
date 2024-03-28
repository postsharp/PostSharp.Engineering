using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using Spectre.Console.Cli;
using System.Collections.Generic;

namespace PostSharp.Engineering.BuildTools.Build.Model;

public abstract class ProductExtension
{
    internal abstract bool AddTeamcityBuildConfiguration( BuildContext context, List<TeamCityBuildConfiguration> teamCityBuildConfigurations );

    internal abstract bool AddTool( IConfigurator<CommandSettings> tools );
}