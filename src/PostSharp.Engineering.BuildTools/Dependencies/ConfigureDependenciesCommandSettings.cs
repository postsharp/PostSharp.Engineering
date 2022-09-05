// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Dependencies;

/// <summary>
/// Base class for <see cref="SetDependenciesCommandSettings"/> and <see cref="ResetDependenciesCommandSettings"/>.
/// </summary>
public abstract class ConfigureDependenciesCommandSettings : BaseDependenciesCommandSettings
{
    // We cannot define the command argument here because they have different ordinals in each derived class, so we just define the consuming interface.

    public abstract string[] GetDependencies();

    public abstract bool GetAllFlag();
}