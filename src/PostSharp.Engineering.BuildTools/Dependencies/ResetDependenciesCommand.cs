// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

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
        ResetDependenciesCommandSettings settings,
        out DependenciesOverrideFile? newDependenciesOverrideFile )
    {
        dependenciesOverrideFile.Dependencies.Remove( dependencyDefinition.Name );

        // Update dependency right after resetting.
        if ( !UpdateDependencyCommand.UpdateDependency( context, out newDependenciesOverrideFile ) )
        {
            context.Console.WriteError( $"Could not update '{dependencyDefinition.Name}' dependency after resetting it." );

            return false;
        }

        return true;
    }
}