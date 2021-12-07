﻿using PostSharp.Engineering.BuildTools.Build;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    public class FetchDependenciesCommandSettings : BaseCommandSettings
    {
        [Description(
            "Directory into which dependency repos are expected to be. If not specified, it will default to the base directory of the current repo." )]
        [CommandOption( "--directory" )]
        public string? ReposDirectory { get; set; }
    }
}