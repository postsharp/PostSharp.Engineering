// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build;

public class TeamCityBuildCommandSettings : BaseBuildSettings
{
    [Description( $"Set specific type of TeamCity build to schedule (Build | Deploy | Bump)" )]
    [CommandArgument( 0, "<BuildType>" )]
    public TeamCityBuildType TeamCityBuildType { get; set; }

    [Description( "Set specific product to schedule build for." )]
    [CommandArgument( 1, "<Product.Name>" )]
    public string? ProductName { get; set; }
}