using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    public class ConfigureDependenciesCommandSettings : BaseCommandSettings
    {
        [Description( "The source of dependencies: Default | Local | BuildServer" )]
        [CommandArgument( 0, "<source>" )]
        public DependencySourceKind Source { get; protected set; }

        [Description( "The list of dependencies (given by name by or position in the dependency list) to configure" )]
        [CommandArgument( 1, "[dependencies]" )]
        public string[] Dependencies { get; protected set; } = Array.Empty<string>();

        [Description( "Specifies that all dependencies must be configured" )]
        [CommandOption( "--all" )]
        public bool All { get; protected set; }

        [Description(
            "Specifies the branch from which the build servers artifacts should be downloaded, when the source is set to BuildServer. Ignored when the --buildNumber is specified." )]
        [CommandOption( "--branch" )]
        public string? Branch { get; protected set; }

        [Description(
            "Specifies the build number of the build of which the artifacts should be downloaded, when the source is set to BuildServer. The --branch is ignored when this is specified." )]
        [CommandOption( "--buildNumber" )]
        public int? BuildNumber { get; protected set; }

        [Description( "Specifies the build type ID of the build of which the artifacts should be downloaded, when the source is set to BuildServer." )]
        [CommandOption( "--buildTypeId" )]
        public string? CiBuildTypeId { get; protected set; }

        [Description(
            "Specifies the name of the dependency which defines the build of which the artifacts should be downloaded, when the source is set to BuildServer. Ignored when the --branch or --buildNumber is specified." )]
        [CommandOption( "--transitive" )]
        public string? VersionDefiningDependencyName { get; protected set; }
    }
}