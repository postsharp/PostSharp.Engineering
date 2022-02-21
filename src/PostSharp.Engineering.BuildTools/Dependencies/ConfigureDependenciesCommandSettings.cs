namespace PostSharp.Engineering.BuildTools.Dependencies;

/// <summary>
/// Base class for <see cref="SetDependenciesCommandSettings"/> and <see cref="ResetDependenciesCommandSettings"/>.
/// </summary>
public abstract class ConfigureDependenciesCommandSettings : CommonCommandSettings
{
    // We cannot define the command argument here because they have different ordinals in each derived class, so we just define the consuming interface.

    public abstract string[] GetDependencies();

    public abstract bool GetAllFlag();
}