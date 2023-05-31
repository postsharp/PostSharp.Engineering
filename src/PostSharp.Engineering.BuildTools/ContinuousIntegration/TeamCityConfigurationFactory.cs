// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using System;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public class TeamCityConfigurationFactory : ICiConfigurationFactory
{
    private readonly BuildConfiguration _debugBuildDependency;
    private readonly BuildConfiguration _releaseBuildDependency;
    private readonly BuildConfiguration _publicBuildDependency;
    private readonly bool _isVersionedTeamCityProject;
    private readonly bool _isCloudInstance;
    private readonly string? _parentCiProjectName;
    private readonly string? _customCiProjectId;

    public TeamCityConfigurationFactory(
        BuildConfiguration debugBuildDependency = BuildConfiguration.Debug,
        BuildConfiguration releaseBuildDependency = BuildConfiguration.Release,
        BuildConfiguration publicBuildDependency = BuildConfiguration.Public,
        string? parentCiProjectName = null,
        string? customCiProjectId = null,
        bool isVersionedTeamCityProject = true,
        bool isCloudInstance = true )
    {
        this._debugBuildDependency = debugBuildDependency;
        this._releaseBuildDependency = releaseBuildDependency;
        this._publicBuildDependency = publicBuildDependency;
        this._parentCiProjectName = parentCiProjectName;
        this._customCiProjectId = customCiProjectId;
        this._isVersionedTeamCityProject = isVersionedTeamCityProject;
        this._isCloudInstance = isCloudInstance;
    }

    public CiConfiguration Create( ProductFamily productFamily, string dependencyNameWithoutDot, bool isVersionedProject )
    {
        var ciProjectId = this._customCiProjectId;

        if ( ciProjectId == null )
        {
            if ( this._parentCiProjectName == null )
            {
                ciProjectId = "";
            }
            else
            {
                ciProjectId = this._parentCiProjectName.Replace( ".", "_", StringComparison.Ordinal );

                if ( this._isVersionedTeamCityProject )
                {
                    ciProjectId = $"{ciProjectId}_{ciProjectId}{productFamily.VersionWithoutDots}";
                }
            }

            ciProjectId = $"{ciProjectId}_{dependencyNameWithoutDot}";
        }

        var buildTypes = new ConfigurationSpecific<string>(
            $"{ciProjectId}_{this._debugBuildDependency}Build",
            $"{ciProjectId}_{this._releaseBuildDependency}Build",
            $"{ciProjectId}_{this._publicBuildDependency}Build" );

        string? versionBumpBuildType = null;

        if ( isVersionedProject )
        {
            versionBumpBuildType = $"{ciProjectId}_VersionBump";
        }

        var deploymentBuildType = $"{ciProjectId}_PublicDeployment";
        string tokenEnvironmentVariableName;
        string baseUrl;

        if ( this._isCloudInstance )
        {
            tokenEnvironmentVariableName = TeamCityHelper.TeamCityCloudTokenEnvironmentVariableName;
            baseUrl = TeamCityHelper.TeamCityCloudUrl;
        }
        else
        {
            tokenEnvironmentVariableName = TeamCityHelper.TeamCityOnPremTokenEnvironmentVariableName;
            baseUrl = TeamCityHelper.TeamCityOnPremUrl;
        }

        return new CiConfiguration( buildTypes, deploymentBuildType, versionBumpBuildType, ciProjectId, tokenEnvironmentVariableName, baseUrl );
    }
}