using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build
{
    public class PublishSettings : CommonCommandSettings
    {
        [Description( "Sets the build configuration (Debug | Release | Public) to publish" )]
        [CommandOption( "-c|--configuration" )]
        public BuildConfiguration BuildConfiguration { get; set; }

        [Description( "Prints the command line, but does not execute it" )]
        [CommandOption( "--dry" )]
        public bool Dry { get; protected set; }
    }
}