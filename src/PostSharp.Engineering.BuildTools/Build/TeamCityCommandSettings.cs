using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build;

public class TeamCityCommandSettings : BaseBuildSettings
{
    [Description( $"Set specific type of TeamCity build to schedule (Build | Deploy | Bump)" )]
    [CommandArgument( 0, "<BuildType>" )]
    public BuildType BuildType { get; set; }

    [Description( "Set specific product to schedule build for." )]
    [CommandArgument( 1, "<Product.Name>" )]
    public string? ProductName { get; set; }
}