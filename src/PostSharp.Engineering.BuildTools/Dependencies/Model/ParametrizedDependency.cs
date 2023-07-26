// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

/// <summary>
/// Represents a dependency including the parameter values that can be supplied by the referencing project.
/// </summary>
/// <param name="Definition"></param>
public record ParametrizedDependency( DependencyDefinition Definition )
{
    public ConfigurationSpecific<BuildConfiguration> ConfigurationMapping { get; init; } = new(
        BuildConfiguration.Debug,
        BuildConfiguration.Release,
        BuildConfiguration.Public );

    public string Name => this.Definition.Name;

    public string NameWithoutDot => this.Definition.NameWithoutDot;
}