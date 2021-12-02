using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.CodeStyle
{
    internal class PullCodeStyleSettings : CodeStyleSettings
    {
        [Description( "Name of the branch. The default is 'develop' for push and 'master' for pull." )]
        [CommandOption( "-b|--branch" )]
        public string? Branch { get; init; }
    }
}