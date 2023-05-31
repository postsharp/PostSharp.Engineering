// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public class CiConfiguration
{
    public ConfigurationSpecific<string> BuildTypes { get; }

    public string DeploymentBuildType { get; }

    public string? VersionBumpBuildType { get; }
    
    public string VcsConfigName { get; }
    
    public string TokenEnvironmentVariableName { get; }
        
    public string BaseUrl { get; }

    public CiConfiguration( ConfigurationSpecific<string> buildTypes, string deploymentBuildType, string? versionBumpBuildType, string vcsConfigName, string tokenEnvironmentVariableName, string baseUrl )
    {
        this.BuildTypes = buildTypes;
        this.DeploymentBuildType = deploymentBuildType;
        this.VersionBumpBuildType = versionBumpBuildType;
        this.VcsConfigName = vcsConfigName;
        this.TokenEnvironmentVariableName = tokenEnvironmentVariableName;
        this.BaseUrl = baseUrl;
    }
}