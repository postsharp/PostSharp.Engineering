using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.NuGet
{
    /// <summary>
    /// Settings for <see cref="VerifyPublicPackageCommand"/>.
    /// </summary>
    public class VerifyPublicPackageCommandSettings : CommandSettings
    {
        [Description( "Directory containing the packages" )]
        [CommandArgument( 0, "<directory>" )]
        public string Directory { get; init; } = null!;
    }
}