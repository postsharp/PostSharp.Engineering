﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public class CiProjectConfiguration
{
    public TeamCityProjectId ProjectId { get; }

    public ConfigurationSpecific<string> BuildTypes { get; }

    public string DeploymentBuildType { get; }

    public string? VersionBumpBuildType { get; }

    public string TokenEnvironmentVariableName { get; }
        
    public string BaseUrl { get; }

    public string BuildAgentType { get; }

    public CiProjectConfiguration( TeamCityProjectId projectProjectId, ConfigurationSpecific<string> buildTypes, string deploymentBuildType, string? versionBumpBuildType, string tokenEnvironmentVariableName, string baseUrl, string buildAgentType )
    {
        this.ProjectId = projectProjectId;
        this.BuildTypes = buildTypes;
        this.DeploymentBuildType = deploymentBuildType;
        this.VersionBumpBuildType = versionBumpBuildType;
        this.TokenEnvironmentVariableName = tokenEnvironmentVariableName;
        this.BaseUrl = baseUrl;
        this.BuildAgentType = buildAgentType;
    }
}