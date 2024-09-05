// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build
{
    public class PublishSettings : BuildSettings
    {
        [Description( "Prints the command line, but does not execute it" )]
        [CommandOption( "--dry" )]
        public bool Dry { get; protected set; }
        
        [Description( "Avoids check of the current branch" )]
        [CommandOption( "--standalone" )]
        public bool IsStandalone { get; protected set; }
    }
}