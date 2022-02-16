using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build
{
    public class BuildSettings : BaseBuildSettings
    {
        [Description( "Signs the assemblies and packages" )]
        [CommandOption( "--sign" )]
        public bool Sign { get; set; }

        [Description( "Creates a zip file with all artifacts" )]
        [CommandOption( "--zip" )]
        public bool CreateZip { get; set; }

        [Description( "Analyzes the test coverage while and after running the tests" )]
        [CommandOption( "--analyze-coverage" )]
        public bool AnalyzeCoverage { get; set; }

        // The following option is used e.g. when testing the LinqPad driver. It is not included by default because of 
        // performance of the normal build scenario.
        
        [Description( "Creates a directory with all packages of the current repo and all transitive dependencies." )]
        [CommandOption( "--consolidated" )]
        public bool CreateConsolidatedDirectory { get; set; }
    }
}