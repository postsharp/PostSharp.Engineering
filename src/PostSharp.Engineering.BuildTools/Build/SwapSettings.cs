using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build
{
    public class SwapSettings : BaseCommandSettings
    {
        [Description( "Prints the command line, but does not execute it" )]
        [CommandOption( "--dry" )]
        public bool Dry { get; protected set; }
    }
}