using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.CodeStyle
{
    internal class CodeStyleSettings : BaseCommandSettings
    {
        [Description( "Clones the repo if it does not exist." )]
        [CommandOption( "--create" )]
        public bool Create { get; init; }

        [Description( "Remote URL of the repo." )]
        [CommandOption( "-u|--url" )]
        public string Url { get; init; } =
            "https://github.com/postsharp/PostSharp.Engineering.CodeStyle.git";
    }
}