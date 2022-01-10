﻿using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies;

public class ResetDependenciesCommand : ConfigureDependenciesCommand<ResetDependenciesCommandSettings>
{
    protected override bool ConfigureDependency(
        BuildContext context,
        VersionsOverrideFile versionsOverrideFile,
        DependencyDefinition dependencyDefinition,
        ResetDependenciesCommandSettings settings )
    {
        versionsOverrideFile.Dependencies.Remove( dependencyDefinition.Name );

        return true;
    }
}