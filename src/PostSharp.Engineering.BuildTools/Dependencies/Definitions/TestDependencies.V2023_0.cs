// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static partial class TestDependencies
{
    // ReSharper disable once InconsistentNaming

    [PublicAPI]
    public static class V2023_0
    {
        private const string _gitHubDevBranch = "dev";
        private const string _gitHubReleaseBranch = "master";
        private const string _azureDevBranch = "master";
        private const string? _azureReleaseBranch = null;

        public static ProductFamily ProductFamily { get; } = new( "2023.0" ) { DownstreamProductFamily = TestDependencies.V2023_1.ProductFamily };

        public static DependencyDefinition TestProduct { get; } = new(
            ProductFamily,
            "PostSharp.Engineering.Test.TestProduct",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Engineering" )
        {
            CiBuildTypes = new ConfigurationSpecific<string>(
                "Test_PostSharpEngineeringTestTestProduct_DebugBuild",
                "Test_PostSharpEngineeringTestTestProduct_ReleaseBuild",
                "Test_PostSharpEngineeringTestTestProduct_PublicBuild" ),
            DeploymentBuildType = "Test_PostSharpEngineeringTestTestProduct_PublicDeployment",
            BumpBuildType = "Test_PostSharpEngineeringTestTestProduct_VersionBump"
        };

        public static DependencyDefinition Dependency { get; } = new(
            ProductFamily,
            "PostSharp.Engineering.Test.Dependency",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Engineering" )
        {
            CiBuildTypes = new ConfigurationSpecific<string>(
                "Test_PostSharpEngineeringTestDependency_DebugBuild",
                "Test_PostSharpEngineeringTestDependency_ReleaseBuild",
                "Test_PostSharpEngineeringTestDependency_PublicBuild" ),
            DeploymentBuildType = "Test_PostSharpEngineeringTestDependency_PublicDeployment",
            BumpBuildType = "Test_PostSharpEngineeringTestDependency_VersionBump"
        };

        public static DependencyDefinition TransitiveDependency { get; } = new(
            ProductFamily,
            "PostSharp.Engineering.Test.TransitiveDependency",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Engineering" )
        {
            CiBuildTypes = new ConfigurationSpecific<string>(
                "Test_PostSharpEngineeringTestTransitiveDependency_DebugBuild",
                "Test_PostSharpEngineeringTestTransitiveDependency_ReleaseBuild",
                "Test_PostSharpEngineeringTestTransitiveDependency_PublicBuild" ),
            DeploymentBuildType = "Test_PostSharpEngineeringTestTransitiveDependency_PublicDeployment",
            BumpBuildType = "Test_PostSharpEngineeringTestTransitiveDependency_VersionBump"
        };

        public static DependencyDefinition GitHub { get; } = new(
            ProductFamily,
            "PostSharp.Engineering.Test.GitHub",
            _gitHubDevBranch,
            _gitHubReleaseBranch,
            VcsProvider.GitHub,
            "postsharp" )
        {
            CiBuildTypes = new ConfigurationSpecific<string>(
                "Test_PostSharpEngineeringTestGitHub_DebugBuild",
                "Test_PostSharpEngineeringTestGitHub_ReleaseBuild",
                "Test_PostSharpEngineeringTestGitHub_PublicBuild" ),
            DeploymentBuildType = "Test_PostSharpEngineeringTestGitHub_PublicDeployment",
            BumpBuildType = "Test_PostSharpEngineeringTestGitHub_VersionBump"
        };

        public static DependencyDefinition MainVersionDependency { get; } = new(
            ProductFamily,
            "PostSharp.Engineering.Test.MainVersionDependency",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Engineering" )
        {
            CiBuildTypes = new ConfigurationSpecific<string>(
                "Test_PostSharpEngineeringTestMainVersionDependency_DebugBuild",
                "Test_PostSharpEngineeringTestMainVersionDependency_ReleaseBuild",
                "Test_PostSharpEngineeringTestMainVersionDependency_PublicBuild" ),
            DeploymentBuildType = "Test_PostSharpEngineeringTestMainVersionDependency_PublicDeployment",
            BumpBuildType = "Test_PostSharpEngineeringTestMainVersionDependency_VersionBump"
        };

        public static DependencyDefinition PatchVersion { get; } = new(
            ProductFamily,
            "PostSharp.Engineering.Test.PatchVersion",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Engineering" )
        {
            CiBuildTypes = new ConfigurationSpecific<string>(
                "Test_PostSharpEngineeringTestPatchVersion_DebugBuild",
                "Test_PostSharpEngineeringTestPatchVersion_ReleaseBuild",
                "Test_PostSharpEngineeringTestPatchVersion_PublicBuild" ),
            DeploymentBuildType = "Test_PostSharpEngineeringPatchVersion_PublicDeployment",
            BumpBuildType = "Test_PostSharpEngineeringTestPatchVersion_VersionBump"
        };
    }
}