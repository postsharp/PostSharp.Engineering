// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

public static partial class TestDependencies
{
    // ReSharper disable once InconsistentNaming

    [PublicAPI]
    public static class V2023_2
    {
        private class TestDependencyDefinition : DependencyDefinition
        {
            public TestDependencyDefinition(
                string dependencyName,
                VcsProvider vcsProvider,
                bool isVersioned = true,
                BuildConfiguration debugBuildDependency = BuildConfiguration.Debug,
                BuildConfiguration releaseBuildDependency = BuildConfiguration.Release,
                BuildConfiguration publicBuildDependency = BuildConfiguration.Public )
                : base(
                    Family,
                    dependencyName,
                    $"develop/{Family.Version}",
                    $"release/{Family.Version}",
                    CreateEngineeringVcsRepository( dependencyName, vcsProvider ),
                    TeamCityHelper.CreateConfiguration(
                        TeamCityHelper.GetProjectIdWithParentProjectId(
                            dependencyName,
                            $"Test_Test{Family.VersionWithoutDots}" ),
                        "caravela04cloud",
                        isVersioned,
                        debugBuildDependency,
                        releaseBuildDependency,
                        publicBuildDependency ) ) { }
        }

        public static ProductFamily Family { get; } = new( "2023.2", DevelopmentDependencies.Family ); // { DownstreamProductFamily = V2023_3.Family };

        public static DependencyDefinition TestProduct { get; } = new TestDependencyDefinition(
            "PostSharp.Engineering.Test.TestProduct",
            VcsProvider.AzureDevOps );

        public static DependencyDefinition Dependency { get; } = new TestDependencyDefinition(
            "PostSharp.Engineering.Test.Dependency",
            VcsProvider.AzureDevOps );
        
        public static DependencyDefinition DependencyOfDependency { get; } = new TestDependencyDefinition(
            "PostSharp.Engineering.Test.DependencyOfDependency",
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