﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools;
using Spectre.Console.Cli;

namespace PostSharp.Engineering.DocFx;

public class DocFxSettings : CommonCommandSettings
{
    [CommandArgument( 0, "<config-path>" )]
    public string ConfigurationPath { get; set; } = null!;
}