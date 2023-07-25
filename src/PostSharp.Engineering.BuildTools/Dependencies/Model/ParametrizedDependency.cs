using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;

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

    public string GetPrivateArtifactsDirectory( BuildConfiguration configuration )
    {
        var dependencyConfiguration = this.ConfigurationMapping[configuration];

        return this.Definition.PrivateArtifactsDirectory.ToString(
            new BuildInfo( null, dependencyConfiguration.ToString().ToLowerInvariant(), this.Definition.MSBuildConfiguration[dependencyConfiguration] ) );
    }
}