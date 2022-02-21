using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.CodeStyle
{
    /// <summary>
    /// Settings of <see cref="PullCodeStyleCommand"/>.
    /// </summary>
    internal class PullCodeStyleSettings : CodeStyleSettings
    {
        [Description( "Name of the branch. The default is 'develop' for push and 'master' for pull." )]
        [CommandOption( "-b|--branch" )]
        public string? Branch { get; init; }
    }
}