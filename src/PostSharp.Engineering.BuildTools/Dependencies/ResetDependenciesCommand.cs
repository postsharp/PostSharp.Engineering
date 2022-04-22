using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies;

/// <summary>
/// Removes the configuration of a dependency from the version file.
/// </summary>
public class ResetDependenciesCommand : ConfigureDependenciesCommand<ResetDependenciesCommandSettings>
{
    protected override bool ConfigureDependency(
        BuildContext context,
        DependenciesOverrideFile dependenciesOverrideFile,
        DependencyDefinition dependencyDefinition,
        ResetDependenciesCommandSettings settings )
    {
        dependenciesOverrideFile.Dependencies.Remove( dependencyDefinition.Name );

        return true;
    }
}