// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using System;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

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
                BuildConfiguration debugBuildDependency = BuildConfiguration.Debug,
                BuildConfiguration releaseBuildDependency = BuildConfiguration.Release,
                BuildConfiguration publicBuildDependency = BuildConfiguration.Public,
                string? customCiProjectName = null )
                : base(
                    Family,
                    dependencyName,
                    GetDevBranch( vcsProvider ),
                    GetReleaseBranch( vcsProvider ),
                    CreateEngineeringVcsRepository( dependencyName, vcsProvider ),
                    TeamCityHelper.CreateConfiguration(
                        TeamCityHelper.GetProjectIdWithParentProjectId(
                            dependencyName,
                            $"Test_Test{Family.VersionWithoutDots}" ),
                        "caravela04",
                        isVersioned,
                        debugBuildDependency,
                        releaseBuildDependency,
                        publicBuildDependency,
                        false ) ) { }
        }

        public static ProductFamily Family { get; } = new( "2023.0", DevelopmentDependencies.Family ) { DownstreamProductFamily = V2023_1.Family };

        private static string GetDevBranch( VcsProvider vcsProvider )
            => vcsProvider switch
            {
                VcsProvider.GitHub => "dev",
                VcsProvider.AzureDevOps => "master",
                _ => throw new InvalidOperationException( $"Unknown VCS provider name: '{vcsProvider}'" )
            };

        private static string? GetReleaseBranch( VcsProvider vcsProvider )
            => vcsProvider switch
            {
                VcsProvider.GitHub => "master",
                VcsProvider.AzureDevOps => null,
                _ => throw new InvalidOperationException( $"Unknown VCS provider name: '{vcsProvider}'" )
            };

        public static DependencyDefinition TestProduct { get; } = new TestDependencyDefinition(
            "PostSharp.Engineering.Test.TestProduct",
            VcsProvider.AzureDevOps );

        public static DependencyDefinition Dependency { get; } = new TestDependencyDefinition(
            "PostSharp.Engineering.Test.Dependency",
            VcsProvider.AzureDevOps );

        public static DependencyDefinition TransitiveDependency { get; } = new TestDependencyDefinition(
            "PostSharp.Engineering.Test.TransitiveDependency",
            VcsProvider.AzureDevOps );

        public static DependencyDefinition GitHub { get; } = new TestDependencyDefinition(
            "PostSharp.Engineering.Test.GitHub",
            VcsProvider.GitHub );

        public static DependencyDefinition MainVersionDependency { get; } = new TestDependencyDefinition(
            "PostSharp.Engineering.Test.MainVersionDependency",
            VcsProvider.AzureDevOps );

        public static DependencyDefinition PatchVersion { get; } = new TestDependencyDefinition(
            "PostSharp.Engineering.Test.PatchVersion",
            VcsProvider.AzureDevOps );
    }
}