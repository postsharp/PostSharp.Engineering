using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Settings of <see cref="KillCommand"/>.
    /// </summary>
    public class KillCommandSettings : CommonCommandSettings
    {
        [Description( "Prints the process that would be killed, but does not kill it" )]
        [CommandOption( "--dry" )]
        public bool Dry { get; protected set; }
    }
}