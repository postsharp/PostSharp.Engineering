namespace PostSharp.Engineering.BuildTools.Dependencies;

public abstract class ConfigureDependenciesCommandSettings : BaseCommandSettings
{
    // We cannot define the command argument here because they have different ordinals in each derived class, so we just define the consuming interface.

    public abstract string[] GetDependencies();

    public abstract bool GetAllFlag();
}