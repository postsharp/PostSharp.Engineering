// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;

namespace PostSharp.Engineering.BuildTools.Build;

/// <summary>
/// Characteristics of a <see cref="BuildConfiguration"/>.
/// </summary>
/// <param name="MSBuildName">Name of the corresponding MSBuild configuration. For <see cref="BuildConfiguration.Public"/>, this value is <c>Release</c>.</param>
/// <param name="RequiresSigning">Determines whether artifacts in this build configuration must be signed.</param>
/// <param name="BuildTriggers">List of build triggers to be configured in the build server.</param>
/// <param name="PublicPublishers">List of publishers of public artefacts.</param>
/// <param name="PrivatePublishers">List of publishers of private artefacts.</param>
/// <param name="Swappers">List of swappers, i.e. logic that swaps a staging environment into a production environment.</param>
/// <param name="TeamCityBuildName">Name of the TeamCity configuration implementing the <b>Build</b> action.</param>
/// <param name="TeamCityDeploymentName">Name of the TeamCity configuration implementing the <b>Deploy</b> action.</param>
/// <param name="TeamCitySwapName">Name of the TeamCity configuration implementing the <b>Swap</b> action.</param>
public record BuildConfigurationInfo(

    // ReSharper disable once InconsistentNaming
    string MSBuildName,
    bool RequiresSigning = false,
    IBuildTrigger[]? BuildTriggers = null,

    // Publishers for public artifacts.
    Publisher[]? PublicPublishers = null,

    // Publishers for private artifacts.
    Publisher[]? PrivatePublishers = null,
    Swapper[]? Swappers = null,
    string? TeamCityBuildName = null,
    string? TeamCityDeploymentName = null,
    string? TeamCitySwapName = null,
    string[]? AdditionalArtifactRules = null,
    bool ExportsToTeamCityBuild = true,
    bool ExportsToTeamCityDeploy = true );