// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    /// <summary>
    /// Settings for <see cref="FetchDependencyCommand"/>.
    /// </summary>
    public class FetchDependenciesCommandSettings : BaseDependenciesCommandSettings
    {
        [Description(
            "Directory into which dependency repos are expected to be. If not specified, it will default to the base directory of the current repo." )]
        [CommandOption( "--directory" )]
        public string? ReposDirectory { get; set; }

        [Description( "Updates to the latest build" )]
        [CommandOption( "-u|--update" )]
        public bool Update { get; set; }
    }
}