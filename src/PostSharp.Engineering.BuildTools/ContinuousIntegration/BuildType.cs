namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

/// <summary>
/// Enumerates the method that determines what <see cref="BuildTools.Build.TeamCityBuildCommand"/> should do.
/// </summary>
public enum BuildType
{
    /// <summary>
    /// The build will be triggered by TeamCity. <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TriggerTeamCityBuild"/>
    /// and <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TryGetBuildTypeIdFromProductName"/>
    /// </summary>
    Build,
    
    /// <summary>
    /// The deployment will be triggered by TeamCity. <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TriggerTeamCityBuild"/>
    /// and <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TryGetBuildTypeIdFromProductName"/>
    Deploy,
    
    /// <summary>
    /// The version bump will be triggered by TeamCity. <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TriggerTeamCityBuild"/>
    /// and <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TryGetBuildTypeIdFromProductName"/>
    Bump
}