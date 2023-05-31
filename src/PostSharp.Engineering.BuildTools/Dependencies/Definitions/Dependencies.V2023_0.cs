// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static partial class Dependencies
{
    // ReSharper disable once InconsistentNaming

    [PublicAPI]
    public static class V2023_0
    {
        private const string _gitHubDevBranch = "dev";
        private const string _gitHubReleaseBranch = "master";
        private const string _azureDevBranch = "master";
        private const string? _azureReleaseBranch = null;

        public static ProductFamily ProductFamily { get; } = new( "2023.0" ) { DownstreamProductFamily = Dependencies.V2023_1.ProductFamily };
            
        public static DependencyDefinition MetalamaBackstage { get; } = new(
            ProductFamily,
            "Metalama.Backstage",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Metalama" );

        public static DependencyDefinition MetalamaCompiler { get; } = new(
            ProductFamily,
            "Metalama.Compiler",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Metalama" )
        {
            EngineeringDirectory = "eng-Metalama",

            // The release build is intentionally used for the debug configuration because we want dependencies to consume the release
            // build, for performance reasons. The debug build will be used only locally, and for this we don't need a configuration here.
            CiBuildTypes = new ConfigurationSpecific<string>(
                "Metalama_MetalamaCompiler_ReleaseBuild",
                "Metalama_MetalamaCompiler_ReleaseBuild",
                "Metalama_MetalamaCompiler_PublicBuild" ),
            PrivateArtifactsDirectory = "artifacts\\packages\\$(MSSBuildConfiguration)\\Shipping"
        };

        public static DependencyDefinition Metalama { get; } = new(
            ProductFamily,
            "Metalama",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Metalama" );
            
        public static DependencyDefinition MetalamaVsx { get; } = new(
            ProductFamily,
            "Metalama.Vsx",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Metalama" );
            
        public static DependencyDefinition MetalamaExtensions { get; } =
            new( ProductFamily, "Metalama.Extensions", _gitHubDevBranch, _gitHubReleaseBranch, VcsProvider.GitHub, "Metalama" );

        public static DependencyDefinition MetalamaSamples { get; } =
            new( ProductFamily, "Metalama.Samples", _azureDevBranch, _gitHubReleaseBranch, VcsProvider.GitHub, "Metalama", false )
            {
                CodeStyle = "Metalama.Samples"
            };
            
        public static DependencyDefinition MetalamaMigration { get; } = new(
            ProductFamily,
            "Metalama.Migration",
            _gitHubDevBranch,
            _gitHubReleaseBranch,
            VcsProvider.GitHub,
            "Metalama.Migration" );
            
        public static DependencyDefinition MetalamaLinqPad { get; } = new(
            ProductFamily,
            "Metalama.LinqPad",
            _gitHubDevBranch,
            _gitHubReleaseBranch,
            VcsProvider.GitHub,
            "Metalama" );
            
        public static DependencyDefinition MetalamaCommunity { get; } = new(
            ProductFamily,
            "Metalama.Community",
            _gitHubDevBranch,
            _gitHubReleaseBranch,
            VcsProvider.GitHub,
            "Metalama" );

        public static DependencyDefinition MetalamaDocumentation { get; } = new(
            ProductFamily,
            "Metalama.Documentation",
            _gitHubDevBranch,
            _gitHubReleaseBranch,
            VcsProvider.GitHub,
            "Metalama",
            false );

        public static DependencyDefinition MetalamaTry { get; } =
            new( ProductFamily, "Metalama.Try", _azureDevBranch, _azureReleaseBranch, VcsProvider.AzureRepos, "Metalama", false )
            {
                EngineeringDirectory = "eng-Metalama"
            };

        public static DependencyDefinition MetalamaPatterns { get; } = new(
            ProductFamily,
            "Metalama.Patterns",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Metalama" );

        public static DependencyDefinition NopCommerce { get; } = new(
            ProductFamily,
            "Metalama.Tests.NopCommerce",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.GitHub,
            "postsharp",
            false )
        {
            CiBuildTypes = new ConfigurationSpecific<string>(
                "Metalama_MetalamaTests_MetalamaTestsNopCommerce_DebugBuild",
                "Metalama_MetalamaTests_MetalamaTestsNopCommerce_ReleaseBuild",
                "Metalama_MetalamaTests_MetalamaTestsNopCommerce_PublicBuild" ),
            DeploymentBuildType = "Metalama_MetalamaTests_MetalamaTestsNopCommerce_PublicDeployment",
            BumpBuildType = "Metalama_MetalamaTests_MetalamaTestsNopCommerce_VersionBump"
        };

        public static DependencyDefinition CargoSupport { get; } = new(
            ProductFamily,
            "Metalama.Tests.CargoSupport",
            _azureDevBranch,
            _azureReleaseBranch,
            VcsProvider.AzureRepos,
            "Metalama",
            false )
        {
            CiBuildTypes = new ConfigurationSpecific<string>(
                "Metalama_MetalamaTests_MetalamaTestsCargoSupport_DebugBuild",
                "Metalama_MetalamaTests_MetalamaTestsCargoSupport_ReleaseBuild",
                "Metalama_MetalamaTests_MetalamaTestsCargoSupport_PublicBuild" ),
            DeploymentBuildType = "Metalama_MetalamaTests_MetalamaTestsCargoSupport_PublicDeployment",
            BumpBuildType = "Metalama_MetalamaTests_MetalamaTestsCargoSupport_VersionBump"
        };
    }
}