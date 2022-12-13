// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.CodeStyle;

internal class ProcessInspectOutputCommandSettings : CommonCommandSettings
{
    [Description( "The xml output file of the inspect command" )]
    [CommandArgument( 0, "<file>" )]
    public string Path { get; init; } = null!;
}