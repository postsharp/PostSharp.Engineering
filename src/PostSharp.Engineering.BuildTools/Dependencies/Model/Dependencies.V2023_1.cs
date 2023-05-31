// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public static partial class Dependencies
    {
        // ReSharper disable once InconsistentNaming

        [PublicAPI]
        public static class V2023_1
        {
            private const string _githubDevBranch = "dev/2023.1";
            private const string _azureDevBranch = "release/2023.1";

            public static ProductFamily ProductFamily { get; } = new( "2023.1" );

            public static DependencyDefinition MetalamaCompiler { get; } = new(
                ProductFamily,
                "Metalama.Compiler",
                _azureDevBranch,
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
                VcsProvider.AzureRepos,
                "Metalama" );

            public static DependencyDefinition MetalamaSamples { get; } =
                new( ProductFamily, "Metalama.Samples", _azureDevBranch, VcsProvider.GitHub, "Metalama", false ) { CodeStyle = "Metalama.Samples" };

            public static DependencyDefinition MetalamaDocumentation { get; } = new(
                ProductFamily,
                "Metalama.Documentation",
                _githubDevBranch,
                VcsProvider.GitHub,
                "Metalama",
                false );

            public static DependencyDefinition PostSharpDocumentation { get; } = new(
                ProductFamily,
                "PostSharp.Documentation",
                _githubDevBranch,
                VcsProvider.GitHub,
                "PostSharp",
                false );

            public static DependencyDefinition MetalamaTry { get; } =
                new( ProductFamily, "Metalama.Try", _azureDevBranch, VcsProvider.AzureRepos, "Metalama", false ) { EngineeringDirectory = "eng-Metalama" };

            public static DependencyDefinition MetalamaVsx { get; } = new( ProductFamily, "Metalama.Vsx", _azureDevBranch, VcsProvider.AzureRepos, "Metalama" );

            public static DependencyDefinition MetalamaExtensions { get; } =
                new( ProductFamily, "Metalama.Extensions", _githubDevBranch, VcsProvider.GitHub, "Metalama" );

            // This is only used from the project template.
            public static DependencyDefinition MyProduct { get; } =
                new( ProductFamily, "PostSharp.Engineering.ProjectTemplate", _githubDevBranch, VcsProvider.GitHub, "NONE" );

            public static DependencyDefinition PostSharpEngineering { get; } = new(
                ProductFamily,
                "PostSharp.Engineering",
                _githubDevBranch,
                VcsProvider.GitHub,
                "postsharp" )
            {
                GenerateSnapshotDependency = false,

                // We always use the debug build for engineering.
                CiBuildTypes = new ConfigurationSpecific<string>(
                    "PostSharpEngineering_DebugBuild",
                    "PostSharpEngineering_DebugBuild",
                    "PostSharpEngineering_DebugBuild" )
            };

            public static DependencyDefinition MetalamaBackstage { get; } = new(
                ProductFamily,
                "Metalama.Backstage",
                _azureDevBranch,
                VcsProvider.AzureRepos,
                "Metalama" );

            public static DependencyDefinition BusinessSystems { get; } = new(
                ProductFamily,
                "BusinessSystems",
                _azureDevBranch,
                VcsProvider.AzureRepos,
                "WebsitesAndBusinessSystems",
                false );

            public static DependencyDefinition HelpBrowser { get; } = new(
                ProductFamily,
                "HelpBrowser",
                _azureDevBranch,
                VcsProvider.AzureRepos,
                "WebsitesAndBusinessSystems",
                false );

            public static DependencyDefinition PostSharpWeb { get; } = new(
                ProductFamily,
                "PostSharpWeb",
                _azureDevBranch,
                VcsProvider.AzureRepos,
                "WebsitesAndBusinessSystems",
                false );

            public static DependencyDefinition MetalamaMigration { get; } = new(
                ProductFamily,
                "Metalama.Migration",
                _githubDevBranch,
                VcsProvider.GitHub,
                "Metalama.Migration" );

            public static DependencyDefinition MetalamaPatterns { get; } = new(
                ProductFamily,
                "Metalama.Patterns",
                _azureDevBranch,
                VcsProvider.AzureRepos,
                "Metalama" );

            public static DependencyDefinition MetalamaCommunity { get; } = new(
                ProductFamily,
                "Metalama.Community",
                _githubDevBranch,
                VcsProvider.GitHub,
                "Metalama" );

            public static DependencyDefinition MetalamaLinqPad { get; } = new(
                ProductFamily,
                "Metalama.LinqPad",
                _githubDevBranch,
                VcsProvider.GitHub,
                "Metalama" );

            public static DependencyDefinition NopCommerce { get; } = new(
                ProductFamily,
                "Metalama.Tests.NopCommerce",
                _azureDevBranch,
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
        }
    }
}