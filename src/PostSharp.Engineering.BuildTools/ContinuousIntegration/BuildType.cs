using PostSharp.Engineering.BuildTools.Build;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

/// <summary>
/// Enumerates the method that determines what <see cref="BuildTools.Build.TeamCityBuildCommand"/> or <see cref="BuildTools.Build.TeamCityDeployCommand"/> or <see cref="TeamCityBuildCommand"/> 
/// should do.
/// </summary>
public enum BuildType
{
    Build,
    
    Deploy,
    
    Bump,
    
    // TODO: remove after
    Test
}