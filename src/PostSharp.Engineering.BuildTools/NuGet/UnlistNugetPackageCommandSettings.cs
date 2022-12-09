﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.NuGet;

public class UnlistNugetPackageCommandSettings : CommonCommandSettings
{
    [Description( "NuGet package name." )]
    [CommandArgument( 0, "<Package.Name>" )]
    public string? PackageName { get; set; }
}