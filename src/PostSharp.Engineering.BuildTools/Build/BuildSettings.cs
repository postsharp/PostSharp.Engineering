using PostSharp.Engineering.BuildTools.Build.Model;
using Spectre.Console.Cli;
using System;
using System.Collections.Immutable;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Settings for <see cref="BuildCommand"/>, <see cref="PrepareCommand"/> and <see cref="CleanCommand"/>.
    /// </summary>
    public class BuildSettings : CommonCommandSettings
    {
        private BuildConfiguration? _resolvedConfiguration;

        [Description( "Sets the build configuration (Debug | Release | Public)" )]
        [CommandOption( "-c|--configuration" )]

        public BuildConfiguration? BuildConfiguration
        {
            [Obsolete( "Use ResolvedBuildConfiguration in consuming code." )]
            get;
            set;
        }

#pragma warning disable CS0618

        public BuildConfiguration ResolvedBuildConfiguration
            => this._resolvedConfiguration ?? this.BuildConfiguration
                ?? throw new InvalidOperationException( "Call the Initialize method or set the BuildConfiguration first ." );

        public override void Initialize( BuildContext context )
        {
            if ( this.BuildConfiguration != null )
            {
                this._resolvedConfiguration = this.BuildConfiguration.Value;
            }
            else
            {
                var defaultConfiguration = context.Product.ReadDefaultConfiguration( context );

                if ( defaultConfiguration == null )
                {
                    context.Console.WriteMessage( $"Using the default configuration Debug." );

                    this._resolvedConfiguration = Build.BuildConfiguration.Debug;
                }
                else
                {
                    context.Console.WriteMessage( $"Using the prepared build configuration: {defaultConfiguration.Value}." );

                    this._resolvedConfiguration = defaultConfiguration.Value;
                }
            }
        }
#pragma warning restore CS0618

        [Description( "Creates a numbered build (typically for an internal CI build). This option is ignored when the build configuration is 'Public'." )]
        [CommandOption( "--buildNumber" )]
        public int BuildNumber { get; set; }

        [Description( "Sets the verbosity" )]
        [CommandOption( "-v|--verbosity" )]
        [DefaultValue( Verbosity.Minimal )]
        public Verbosity Verbosity { get; set; }

        [Description( "Executes only the current command, but not the previous command" )]
        [CommandOption( "--no-dependencies" )]
        public bool NoDependencies { get; set; }

        [Description( "Determines wether test-only assemblies should be included in the operation" )]
        [CommandOption( "--include-tests" )]
        public bool IncludeTests { get; set; }

        [Description( "Disables concurrent processing" )]
        [CommandOption( "--no-concurrency" )]
        public bool NoConcurrency { get; set; }

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
            => configuration == Build.BuildConfiguration.Public
                ? new VersionSpec( VersionKind.Public )
                : this.BuildNumber > 0
                    ? new VersionSpec( VersionKind.Numbered, this.BuildNumber )
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