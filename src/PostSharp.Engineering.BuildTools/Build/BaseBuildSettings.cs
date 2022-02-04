using PostSharp.Engineering.BuildTools.Build.Model;
using Spectre.Console.Cli;
using System.Collections.Immutable;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build
{
    public class BaseBuildSettings : BaseCommandSettings
    {
        [Description( "Sets the build configuration (Debug | Release | Public)" )]
        [CommandOption( "-c|--configuration" )]
        public BuildConfiguration BuildConfiguration { get; set; }

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

        public BaseBuildSettings WithIncludeTests( bool value )
        {
            var clone = (BaseBuildSettings) this.MemberwiseClone();
            clone.IncludeTests = value;

            return clone;
        }

        public BaseBuildSettings WithoutConcurrency()
        {
            var clone = (BaseBuildSettings) this.MemberwiseClone();
            clone.NoConcurrency = true;

            return clone;
        }

        public BaseBuildSettings WithAdditionalProperties( ImmutableDictionary<string, string> properties )
        {
            if ( properties.IsEmpty )
            {
                return this;
            }

            var clone = (BaseBuildSettings) this.MemberwiseClone();
            clone.Properties = clone.Properties.AddRange( properties );

            return clone;
        }

        public VersionSpec VersionSpec
            => this.BuildConfiguration == BuildConfiguration.Public
                ? new VersionSpec( VersionKind.Public )
                : this.BuildNumber > 0
                    ? new VersionSpec( VersionKind.Numbered, this.BuildNumber )
                    : new VersionSpec( VersionKind.Local );
    }
}