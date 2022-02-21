using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Coverage
{
    /// <summary>
    /// Settings for <see cref="AnalyzeCoverageCommand"/>.
    /// </summary>
    public class AnalyzeCoverageCommandSettings : CommandSettings
    {
        [CommandArgument( 0, "<path>" )]
        [Description( "Path to the OpenCover xml file" )]
        public string Path { get; init; } = null!;
    }
}