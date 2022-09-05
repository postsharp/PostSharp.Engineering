// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Spectre.Console.Cli;
using System;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Dependencies;

/// <summary>
/// Settings of <see cref="ResetDependenciesCommand"/>.
/// </summary>
public class ResetDependenciesCommandSettings : ConfigureDependenciesCommandSettings
{
    [Description( "The list of dependencies (given by name by or position in the dependency list) to configure" )]
    [CommandArgument( 1, "[dependencies]" )]
    public string[] Dependencies { get; protected set; } = Array.Empty<string>();

    [Description( "Specifies that all dependencies must be configured" )]
    [CommandOption( "--all" )]
    public bool All { get; protected set; }

    public override string[] GetDependencies() => this.Dependencies;

    public override bool GetAllFlag() => this.All;
}