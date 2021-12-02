using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build
{
    public class BuildOptions : BaseBuildSettings
    {
        [Description( "Signs the assemblies and packages" )]
        [CommandOption( "--sign" )]
        public bool Sign { get; set; }

        [Description( "Create a zip file with all artifacts" )]
        [CommandOption( "--zip" )]
        public bool CreateZip { get; set; }

        [Description( "Analyze the test coverage while and after running the tests" )]
        [CommandOption( "--analyze-coverage" )]
        public bool AnalyzeCoverage { get; set; }
    }
}