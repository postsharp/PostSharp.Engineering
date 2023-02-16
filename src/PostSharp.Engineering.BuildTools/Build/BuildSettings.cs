// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using Spectre.Console.Cli;
using System.Collections.Immutable;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Settings for <see cref="BuildCommand"/>, <see cref="PrepareCommand"/> and <see cref="CleanCommand"/>.
    /// </summary>
    public class BuildSettings : BaseBuildSettings
    {
        private int? _solutionId;

        [Description( "Creates a numbered build (typically for an internal CI build). This option is ignored when the build configuration is 'Public'." )]
        [CommandOption( "--buildNumber" )]
        public int? BuildNumber { get; set; }

        [Description( "Specifies the name of the build type in TeamCity." )]
        [CommandOption( "--buildType" )]
        public string? BuildType { get; set; }

        [Description( "Sets the verbosity" )]
        [CommandOption( "-v|--verbosity" )]
        [DefaultValue( Verbosity.Minimal )]
        public Verbosity Verbosity { get; set; }

        [Description( "Executes only the current command, but not the previous command" )]
        [CommandOption( "--no-dependencies" )]
        public bool NoDependencies { get; set; }

        [Description( "Determines whether test-only assemblies should be included in the operation" )]
        [CommandOption( "--include-tests" )]
        public bool IncludeTests { get; set; }

        [Description( "Disables concurrent processing" )]
        [CommandOption( "--no-concurrency" )]
        public bool NoConcurrency { get; set; }

        [Description(
            "Specifies the solution that should be built. The list of solutions and their id is given by the `list-solutions` command." +
            "Setting this property automatically adds the --no-dependencies option." )]
        [CommandOption( "--solution" )]
        public int? SolutionId
        {
            get => this._solutionId;
            set
            {
                this._solutionId = value;
                this.NoDependencies = true;
            }
        }

        public BuildSettings WithIncludeTests( bool value )
        {
            var clone = (BuildSettings) this.MemberwiseClone();
            clone.IncludeTests = value;

            return clone;
        }

        public BuildSettings WithoutConcurrency()
        {
            var clone = (BuildSettings) this.MemberwiseClone();
            clone.NoConcurrency = true;

            return clone;
        }

        public BuildSettings WithAdditionalProperties( ImmutableDictionary<string, string> properties )
        {
            if ( properties.IsEmpty )
            {
                return this;
            }

            var clone = (BuildSettings) this.MemberwiseClone();
            clone.Properties = clone.Properties.AddRange( properties );

            return clone;
        }

        public VersionSpec GetVersionSpec( BuildConfiguration configuration )
            => configuration == BuildConfiguration.Public
                ? new VersionSpec( VersionKind.Public )
                : this.BuildNumber != null
                    ? new VersionSpec( VersionKind.Numbered, this.BuildNumber.Value )
                    : new VersionSpec( VersionKind.Local );

        [Description( "Does not sign the assemblies and packages" )]
        [CommandOption( "--no-sign" )]
        public bool NoSign { get; set; }

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