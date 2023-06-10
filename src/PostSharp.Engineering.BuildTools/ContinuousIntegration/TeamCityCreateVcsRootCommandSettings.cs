// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public class TeamCityCreateVcsRootCommandSettings : CommonCommandSettings
{
    [Description( "The URL of the new VCS root." )]
    [CommandArgument( 0, "<url>" )]
    public string Url { get; init; } = null!;
    
    [Description( "The version of the new realted product used in branch specs." )]
    [CommandArgument( 2, "<version>" )]
    public string Version { get; init; } = null!;
    
    [Description( "The id of the parent project. Skip for the root project." )]
    [CommandArgument( 3, "[projectId]" )]
    public string? ProjectId { get; init; } = null;
}