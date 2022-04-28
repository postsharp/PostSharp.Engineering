using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build;

public class TeamCityCommandSettings : BaseBuildSettings
{
    [Description( "Bump the product before Deployment" )]
    [CommandOption( "--bump" )]
    public bool Bump { get; protected set; }
    
    [Description( "Set specific ProductName to deploy/bump." )]
    [CommandArgument( 0, "<BuildType>" )]
    public BuildType BuildType { get; set; }

    [Description( "Set specific ProductName to deploy/bump." )]
    [CommandArgument( 1, "[Product.Name]" )]
    public string? ProductName { get; set; }
}