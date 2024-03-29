﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Search;

public class TypesenseCommandSettings : CommandSettings
{
    [Description( "Typesense protocol, host and port, e.g. http://localhost:8108" )]
    [CommandArgument( 0, "<typesense-url>" )]
    public string TypesenseUri { get; init; } = null!;
    
    [Description( "Attach the debugger to the process before executing the command" )]
    [CommandOption( "--debug" )]
    public bool Debug { get; init; } = false;
}