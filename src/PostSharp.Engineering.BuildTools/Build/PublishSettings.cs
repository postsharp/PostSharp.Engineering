using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build
{
    public class PublishSettings : BaseCommandSettings
    {
        [Description(
            "Sets the build configuration (Debug or Release) to publish. This option is irrelevant unless the artifact paths depend on the build configuration." )]
        [CommandOption( "-c|--configuration" )]
        public BuildConfiguration BuildConfiguration { get; set; }

        [Description( "Prints the command line, but does not execute it" )]
        [CommandOption( "--dry" )]
        public bool Dry { get; protected set; }
    }
}