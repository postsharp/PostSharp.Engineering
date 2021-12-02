﻿using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.NuGet
{
    public class VerifyPackageSettings : CommandSettings
    {
        [Description( "Directory containing the packages" )]
        [CommandArgument( 0, "<directory>" )]
        public string Directory { get; init; } = null!;
    }
}