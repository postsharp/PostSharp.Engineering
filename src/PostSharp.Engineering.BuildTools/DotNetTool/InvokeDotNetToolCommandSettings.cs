﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools;

public class InvokeDotNetToolCommandSettings : CommonCommandSettings
{
    [Description( "The arguments passed to the tool." )]
    [CommandArgument( 1, "[arguments]" )]
    public string? Arguments { get; init; }
}