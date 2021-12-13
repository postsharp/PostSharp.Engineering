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

        [Description( "Specifies the branch from which the build servers artifacts should be downloaded, when the source is set to BuildServer." )]
        [CommandOption( "--branch" )]
        public string? Branch { get; protected set; }
    }
}