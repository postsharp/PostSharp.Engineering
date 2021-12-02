﻿using PostSharp.Engineering.BuildTools.Build;
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
            "https://postsharp@dev.azure.com/postsharp/Caravela/_git/Caravela.Engineering";
    }
}