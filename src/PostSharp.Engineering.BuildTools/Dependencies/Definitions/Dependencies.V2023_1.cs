// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

public static partial class Dependencies
{
    // ReSharper disable once InconsistentNaming

    [PublicAPI]
    public static class V2023_1
    {
        public static ProductFamily ProductFamily { get; } = new( "2023.1" );

        private static readonly string _devBranch = $"develop/{ProductFamily.Version}";
        private static readonly string _releaseBranch = $"release/{ProductFamily.Version}";

        public static DependencyDefinition MetalamaBackstage { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "Metalama.Backstage",
            VcsProvider.AzureRepos,
            "Metalama" );
        
        public static DependencyDefinition MetalamaCompiler { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "Metalama.Compiler",
            VcsProvider.AzureRepos,
            "Metalama" )
        {
            EngineeringDirectory = "eng-Metalama",

            // The release build is intentionally used for the debug configuration because we want dependencies to consume the release
            // build, for performance reasons. The debug build will be used only locally, and for this we don't need a configuration here.
            CiBuildTypes = new ConfigurationSpecific<string>(
                $"Metalama_Metalama{ProductFamily.VersionWithoutDot}_MetalamaCompiler_ReleaseBuild",
                $"Metalama_Metalama{ProductFamily.VersionWithoutDot}_MetalamaCompiler_ReleaseBuild",
                $"Metalama_Metalama{ProductFamily.VersionWithoutDot}_MetalamaCompiler_PublicBuild" ),
            PrivateArtifactsDirectory = "artifacts\\packages\\$(MSSBuildConfiguration)\\Shipping"
        };

        public static DependencyDefinition Metalama { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "Metalama",
            VcsProvider.AzureRepos,
            "Metalama" );
        
        public static DependencyDefinition MetalamaVsx { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "Metalama.Vsx",
            VcsProvider.AzureRepos,
            "Metalama" );
        
        public static DependencyDefinition MetalamaExtensions { get; } =
            new(
                ProductFamily,
                _devBranch,
                _releaseBranch,
                "Metalama.Extensions",
                VcsProvider.GitHub,
                "Metalama" );

        public static DependencyDefinition MetalamaSamples { get; } =
            new(
                ProductFamily,
                _devBranch,
                _releaseBranch,
                "Metalama.Samples",
                VcsProvider.GitHub,
                "Metalama",
                false ) { CodeStyle = "Metalama.Samples" };
        
        public static DependencyDefinition MetalamaMigration { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "Metalama.Migration",
            VcsProvider.GitHub,
            "Metalama" );
        
        public static DependencyDefinition MetalamaLinqPad { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "Metalama.LinqPad",
            VcsProvider.GitHub,
            "Metalama" );
        
        public static DependencyDefinition MetalamaCommunity { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "Metalama.Community",
            VcsProvider.GitHub,
            "Metalama" );

        public static DependencyDefinition MetalamaDocumentation { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "Metalama.Documentation",
            VcsProvider.GitHub,
            "Metalama",
            false );

        public static DependencyDefinition MetalamaTry { get; } =
            new(
                ProductFamily,
                _devBranch,
                _releaseBranch,
                "Metalama.Try",
                VcsProvider.AzureRepos,
                "Metalama",
                false ) { EngineeringDirectory = "eng-Metalama" };

        public static DependencyDefinition MetalamaPatterns { get; } = new(
            ProductFamily,
            _devBranch,
            _releaseBranch,
            "Metalama.Patterns",
            VcsProvider.AzureRepos,
            "Metalama" );
        
        public static DependencyDefinition NopCommerce { get; } = new(
            ProductFamily,
            "Metalama.Tests.NopCommerce",
            _devBranch,
            _releaseBranch,
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
            _devBranch,
            _releaseBranch,
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