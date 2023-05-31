// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using System;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static partial class MetalamaDependencies
{
    // ReSharper disable once InconsistentNaming

    [PublicAPI]
    public static class V2023_0
    {
        public class MetalamaDependencyDefinition : DependencyDefinition
        {
            public MetalamaDependencyDefinition(
                string dependencyName,
                VcsProvider vcsProvider,
                bool isVersioned = true,
                string vcsProjectName = "Metalama",
                string? ciProjectName = null,
                BuildConfiguration debugBuildDependency = BuildConfiguration.Debug,
                BuildConfiguration releaseBuildDependency = BuildConfiguration.Release,
                BuildConfiguration publicBuildDependency = BuildConfiguration.Public,
                string? customCiProjectName = null )
                : base(
                    Family,
                    dependencyName,
                    GetDevBranch( vcsProvider ),
                    GetReleaseBranch( vcsProvider ),
                    vcsProvider,
                    vcsProjectName,
                    new TeamCityConfigurationFactory(
                        debugBuildDependency,
                        releaseBuildDependency,
                        publicBuildDependency,
                        vcsProjectName,
                         false,
                        false,
                        ciProjectName ?? vcsProjectName,
                        customCiProjectName ),
                    isVersioned ) { }
        }

        public static ProductFamily Family { get; } = new( "2023.0" ) { DownstreamProductFamily = V2023_1.Family };

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

        public static DependencyDefinition MetalamaBackstage { get; } = new MetalamaDependencyDefinition( "Metalama.Backstage", VcsProvider.AzureRepos );

        // The release build is intentionally used for the debug configuration because we want dependencies to consume the release
        // build, for performance reasons. The debug build will be used only locally, and for this we don't need a configuration here.
        public static DependencyDefinition MetalamaCompiler { get; } = new MetalamaDependencyDefinition(
            "Metalama.Compiler",
            VcsProvider.AzureRepos,
            debugBuildDependency: BuildConfiguration.Release )
        {
            EngineeringDirectory = "eng-Metalama", PrivateArtifactsDirectory = Path.Combine( "artifacts", "packages", "$(MSSBuildConfiguration)", "Shipping" )
        };

        public static DependencyDefinition Metalama { get; } = new MetalamaDependencyDefinition( "Metalama", VcsProvider.AzureRepos );

        public static DependencyDefinition MetalamaVsx { get; } = new MetalamaDependencyDefinition( "Metalama.Vsx", VcsProvider.AzureRepos );

        public static DependencyDefinition MetalamaExtensions { get; } = new MetalamaDependencyDefinition( "Metalama.Extensions", VcsProvider.GitHub );

        public static DependencyDefinition MetalamaSamples { get; } =
            new MetalamaDependencyDefinition( "Metalama.Samples", VcsProvider.GitHub, false ) { CodeStyle = "Metalama.Samples" };

        public static DependencyDefinition MetalamaMigration { get; } = new MetalamaDependencyDefinition(
            "Metalama.Migration",
            VcsProvider.GitHub,
            vcsProjectName: "Metalama.Migration" );

        public static DependencyDefinition MetalamaLinqPad { get; } = new MetalamaDependencyDefinition(
            "Metalama.LinqPad",
            VcsProvider.GitHub,
            vcsProjectName: "Metalama" );

        public static DependencyDefinition MetalamaCommunity { get; } = new MetalamaDependencyDefinition(
            "Metalama.Community",
            VcsProvider.GitHub );

        public static DependencyDefinition MetalamaDocumentation { get; } = new MetalamaDependencyDefinition(
            "Metalama.Documentation",
            VcsProvider.GitHub,
            false );

        public static DependencyDefinition MetalamaTry { get; } =
            new MetalamaDependencyDefinition( "Metalama.Try", VcsProvider.AzureRepos, false ) { EngineeringDirectory = "eng-Metalama" };

        public static DependencyDefinition MetalamaPatterns { get; } = new MetalamaDependencyDefinition(
            "Metalama.Patterns",
            VcsProvider.AzureRepos );

        public static DependencyDefinition NopCommerce { get; } = new MetalamaDependencyDefinition(
            "Metalama.Tests.NopCommerce",
            VcsProvider.GitHub,
            false,
            "postsharp",
            customCiProjectName: "Metalama_MetalamaTests" );

        public static DependencyDefinition CargoSupport { get; } = new MetalamaDependencyDefinition(
            "Metalama.Tests.CargoSupport",
            VcsProvider.GitHub,
            false,
            "postsharp",
            customCiProjectName: "Metalama_MetalamaTests" );
    }
}