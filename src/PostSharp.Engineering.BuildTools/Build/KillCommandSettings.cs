using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build
{
    public class KillCommandSettings : BaseCommandSettings
    {
        [Description( "Prints the process that would be killed, but does not kill it" )]
        [CommandOption( "--dry" )]
        public bool Dry { get; protected set; }
    }
}