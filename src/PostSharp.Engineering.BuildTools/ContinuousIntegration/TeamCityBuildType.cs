// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

/// <summary>
/// Enumerates the method that determines what <see cref="BuildTools.Build.TeamCityBuildCommand"/> should do.
/// </summary>
public enum TeamCityBuildType
{
    /// <summary>
    /// The build of product will be scheduled on TeamCity. <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TriggerTeamCityBuild"/>
    /// and <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TryGetBuildTypeIdFromDependencyDefinition"/>
    /// </summary>
    Build,

    /// <summary>
    /// The deployment of product will be scheduled on TeamCity. <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TriggerTeamCityBuild"/>
    /// and <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TryGetBuildTypeIdFromDependencyDefinition"/>
    /// </summary>
    Deploy,

    /// <summary>
    /// The version bump of product will be scheduled on TeamCity. <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TriggerTeamCityBuild"/>
    /// and <see cref="BuildTools.ContinuousIntegration.TeamCityHelper.TryGetBuildTypeIdFromDependencyDefinition"/>
    /// </summary>
    Bump
}