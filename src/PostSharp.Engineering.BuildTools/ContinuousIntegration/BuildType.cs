namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

/// <summary>
/// Enumerates the method that determines what <see cref="BuildTools.Build.TeamCityBuildCommand"/> should do.
/// </summary>
public enum BuildType
{
    /// <summary>
    /// The build of product will be scheduled on TeamCity. <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TriggerTeamCityBuild"/>
    /// and <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TryGetBuildTypeIdFromProductName"/>
    /// </summary>
    Build,
    
    /// <summary>
    /// The deployment of product will be scheduled on TeamCity. <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TriggerTeamCityBuild"/>
    /// and <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TryGetBuildTypeIdFromProductName"/>
    /// </summary>
    Deploy,
    
    /// <summary>
    /// The version bump of product will be scheduled on TeamCity. <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TriggerTeamCityBuild"/>
    /// and <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TryGetBuildTypeIdFromProductName"/>
    /// </summary>
    Bump
}