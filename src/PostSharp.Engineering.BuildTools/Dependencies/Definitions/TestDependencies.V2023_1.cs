// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

public static partial class TestDependencies
{
    // ReSharper disable once InconsistentNaming

    [PublicAPI]
    public static class V2023_1
    {
        public static ProductFamily ProductFamily { get; } = new( "2023.1" );

        private static readonly string _devBranch = $"develop/{ProductFamily.Version}";
        private static readonly string _releaseBranch = $"release/{ProductFamily.Version}";

        public static DependencyDefinition TestProduct { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "PostSharp.Engineering.Test.TestProduct",
            VcsProvider.AzureRepos,
            "Engineering" )
        {
            CiConfiguration = new ConfigurationSpecific<string>(
                $"Test_Test{ProductFamily.VersionWithoutDots}_TestProduct_DebugBuild",
                $"Test_Test{ProductFamily.VersionWithoutDots}_TestProduct_ReleaseBuild",
                $"Test_Test{ProductFamily.VersionWithoutDots}_TestProduct_PublicBuild" ),
            DeploymentBuildType = $"Test_Test{ProductFamily.VersionWithoutDots}_TestProduct_PublicDeployment",
            BumpBuildType = $"Test_Test{ProductFamily.VersionWithoutDots}_TestProduct_VersionBump"
        };

        public static DependencyDefinition Dependency { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "PostSharp.Engineering.Test.Dependency",
            VcsProvider.AzureRepos,
            "Engineering" )
        {
            CiConfiguration = new ConfigurationSpecific<string>(
                $"Test_Test{ProductFamily.VersionWithoutDots}_Dependency_DebugBuild",
                $"Test_Test{ProductFamily.VersionWithoutDots}_Dependency_ReleaseBuild",
                $"Test_Test{ProductFamily.VersionWithoutDots}_Dependency_PublicBuild" ),
            DeploymentBuildType = $"Test_Test{ProductFamily.VersionWithoutDots}_Dependency_PublicDeployment",
            BumpBuildType = $"Test_Test{ProductFamily.VersionWithoutDots}_Dependency_VersionBump"
        };

        public static DependencyDefinition TransitiveDependency { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "PostSharp.Engineering.Test.TransitiveDependency",
            VcsProvider.AzureRepos,
            "Engineering" )
        {
            CiConfiguration = new ConfigurationSpecific<string>(
                $"Test_Test{ProductFamily.VersionWithoutDots}_TransitiveDependency_DebugBuild",
                $"Test_Test{ProductFamily.VersionWithoutDots}_TransitiveDependency_ReleaseBuild",
                $"Test_Test{ProductFamily.VersionWithoutDots}_TransitiveDependency_PublicBuild" ),
            DeploymentBuildType = $"Test_Test{ProductFamily.VersionWithoutDots}_TransitiveDependency_PublicDeployment",
            BumpBuildType = $"Test_Test{ProductFamily.VersionWithoutDots}_TransitiveDependency_VersionBump"
        };

        public static DependencyDefinition GitHub { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "PostSharp.Engineering.Test.GitHub",
            VcsProvider.GitHub,
            "postsharp" )
        {
            CiConfiguration = new ConfigurationSpecific<string>(
                $"Test_Test{ProductFamily.VersionWithoutDots}_GitHub_DebugBuild",
                $"Test_Test{ProductFamily.VersionWithoutDots}_GitHub_ReleaseBuild",
                $"Test_Test{ProductFamily.VersionWithoutDots}_GitHub_PublicBuild" ),
            DeploymentBuildType = $"Test_Test{ProductFamily.VersionWithoutDots}_GitHub_PublicDeployment",
            BumpBuildType = $"Test_Test{ProductFamily.VersionWithoutDots}_GitHub_VersionBump"
        };

        public static DependencyDefinition MainVersionDependency { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "PostSharp.Engineering.Test.MainVersionDependency",
            VcsProvider.AzureRepos,
            "Engineering" )
        {
            CiConfiguration = new ConfigurationSpecific<string>(
                $"Test_Test{ProductFamily.VersionWithoutDots}_MainVersionDependency_DebugBuild",
                $"Test_Test{ProductFamily.VersionWithoutDots}_MainVersionDependency_ReleaseBuild",
                $"Test_Test{ProductFamily.VersionWithoutDots}_MainVersionDependency_PublicBuild" ),
            DeploymentBuildType = $"Test_Test{ProductFamily.VersionWithoutDots}_MainVersionDependency_PublicDeployment",
            BumpBuildType = $"Test_Test{ProductFamily.VersionWithoutDots}_MainVersionDependency_VersionBump"
        };

        public static DependencyDefinition PatchVersion { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "PostSharp.Engineering.Test.PatchVersion",
            VcsProvider.AzureRepos,
            "Engineering" )
        {
            CiConfiguration = new ConfigurationSpecific<string>(
                $"Test_Test{ProductFamily.VersionWithoutDots}_PatchVersion_DebugBuild",
                $"Test_Test{ProductFamily.VersionWithoutDots}_PatchVersion_ReleaseBuild",
                $"Test_Test{ProductFamily.VersionWithoutDots}_PatchVersion_PublicBuild" ),
            DeploymentBuildType = $"Test_Test{ProductFamily.VersionWithoutDots}_PatchVersion_PublicDeployment",
            BumpBuildType = $"Test_Test{ProductFamily.VersionWithoutDots}_PatchVersion_VersionBump"
        };
    }
}