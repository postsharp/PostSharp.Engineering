// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using System;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static partial class TestDependencies
{
    // ReSharper disable once InconsistentNaming

    [PublicAPI]
    public static class V2023_0
    {
        private class TestDependencyDefinition : DependencyDefinition
        {
            public TestDependencyDefinition(
                string dependencyName,
                VcsProvider vcsProvider,
                bool isVersioned = true,
                ICiConfigurationFactory? ciConfigurationFactory = null )
                : base(
                    Family,
                    dependencyName,
                    GetDevBranch( vcsProvider ),
                    GetReleaseBranch( vcsProvider ),
                    vcsProvider,
                    "Test",
                    isVersioned,
                    ciConfigurationFactory ) { }
        }
        
        public static ProductFamily Family { get; } = new( "2023.0" ) { DownstreamProductFamily = TestDependencies.V2023_1.ProductFamily };
        
        private static string GetDevBranch( VcsProvider vcsProvider )
            => vcsProvider.Name switch
            {
                VcsProviderName.GitHub => "dev",
                VcsProviderName.AzureDevOps => "master",
                _ => throw new InvalidOperationException( $"Unknown VCS provider name: '{vcsProvider.Name}'" )
            };

        private static string? GetReleaseBranch( VcsProvider vcsProvider )
            => vcsProvider.Name switch
            {
                VcsProviderName.GitHub => "master",
                VcsProviderName.AzureDevOps => null,
                _ => throw new InvalidOperationException( $"Unknown VCS provider name: '{vcsProvider.Name}'" )
            };

        public static DependencyDefinition TestProduct { get; } = new TestDependencyDefinition(
            "PostSharp.Engineering.Test.TestProduct",
            VcsProvider.AzureRepos );

        public static DependencyDefinition Dependency { get; } = new TestDependencyDefinition(
            "PostSharp.Engineering.Test.Dependency",
            VcsProvider.AzureRepos );

        public static DependencyDefinition TransitiveDependency { get; } = new TestDependencyDefinition(
            "PostSharp.Engineering.Test.TransitiveDependency",
            VcsProvider.AzureRepos );

        public static DependencyDefinition GitHub { get; } = new(
            Family,
            "PostSharp.Engineering.Test.GitHub",
            _gitHubDevBranch,
            _gitHubReleaseBranch,
            VcsProvider.GitHub,
            "postsharp" )
        {
            CiConfiguration = new ConfigurationSpecific<string>(
                "Test_PostSharpEngineeringTestGitHub_DebugBuild",
                "Test_PostSharpEngineeringTestGitHub_ReleaseBuild",
                "Test_PostSharpEngineeringTestGitHub_PublicBuild" ),
            DeploymentBuildType = "Test_PostSharpEngineeringTestGitHub_PublicDeployment",
            BumpBuildType = "Test_PostSharpEngineeringTestGitHub_VersionBump"
        };

        public static DependencyDefinition MainVersionDependency { get; } = new(
            Family,
            "PostSharp.Engineering.Test.MainVersionDependency",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Engineering" )
        {
            CiConfiguration = new ConfigurationSpecific<string>(
                "Test_PostSharpEngineeringTestMainVersionDependency_DebugBuild",
                "Test_PostSharpEngineeringTestMainVersionDependency_ReleaseBuild",
                "Test_PostSharpEngineeringTestMainVersionDependency_PublicBuild" ),
            DeploymentBuildType = "Test_PostSharpEngineeringTestMainVersionDependency_PublicDeployment",
            BumpBuildType = "Test_PostSharpEngineeringTestMainVersionDependency_VersionBump"
        };

        public static DependencyDefinition PatchVersion { get; } = new(
            Family,
            "PostSharp.Engineering.Test.PatchVersion",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Engineering" )
        {
            CiConfiguration = new ConfigurationSpecific<string>(
                "Test_PostSharpEngineeringTestPatchVersion_DebugBuild",
                "Test_PostSharpEngineeringTestPatchVersion_ReleaseBuild",
                "Test_PostSharpEngineeringTestPatchVersion_PublicBuild" ),
            DeploymentBuildType = "Test_PostSharpEngineeringPatchVersion_PublicDeployment",
            BumpBuildType = "Test_PostSharpEngineeringTestPatchVersion_VersionBump"
        };
    }
}