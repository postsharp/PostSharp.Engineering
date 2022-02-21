using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.NuGet
{
    /// <summary>
    /// Settings for <see cref="RenamePackagesCommand"/>.
    /// </summary>
    public class RenamePackageCommandSettings : CommandSettings
    {
        [Description( "Directory containing the packages" )]
        [CommandArgument( 0, "<directory>" )]
        public string Directory { get; init; } = null!;
    }
}