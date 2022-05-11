using System;
using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public static class Dependencies
    {
        public static DependencyDefinition MetalamaCompiler { get; } = new(
            "Metalama.Compiler",
            VcsProvider.AzureRepos,
            "Metalama" )
        {
            // The release build is intentionally used for the debug configuration because we want dependencies to consume the release
            // build, for performance reasons. The debug build will be used only locally, and for this we don't need a configuration here.
            CiBuildTypes = new ConfigurationSpecific<string>(
                "Metalama_MetalamaCompiler_ReleaseBuild",
                "Metalama_MetalamaCompiler_ReleaseBuild",
                "Metalama_MetalamaCompiler_PublicBuild" )
        };

        public static DependencyDefinition Metalama { get; } = new(
            "Metalama",
            VcsProvider.AzureRepos,
            "Metalama" );

        public static DependencyDefinition MetalamaSamples { get; } = new( "Metalama.Samples", VcsProvider.GitHub, "Metalama", false );

        public static DependencyDefinition MetalamaDocumentation { get; } = new( "Metalama.Documentation", VcsProvider.GitHub, "Metalama", false );

        public static DependencyDefinition MetalamaTry { get; } = new( "Metalama.Try", VcsProvider.AzureRepos, "Metalama", false );

        public static DependencyDefinition MetalamaVsx { get; } = new( "Metalama.Vsx", VcsProvider.AzureRepos, "Metalama" );

        public static DependencyDefinition MetalamaOpenAutoCancellationToken { get; } =
            new( "Metalama.Open.AutoCancellationToken", VcsProvider.GitHub, "Metalama" )
            {
                // Metalama.Open.AutoCancellationToken is part of Metalama.Open products group, which is propagated to build types string.
                CiBuildTypes = new ConfigurationSpecific<string>(
                    "Metalama_MetalamaOpen_MetalamaOpenAutoCancellationToken_DebugBuild",
                    "Metalama_MetalamaOpen_MetalamaOpenAutoCancellationToken_ReleaseBuild",
                    "Metalama_MetalamaOpen_MetalamaOpenAutoCancellationToken_PublicBuild" ),
                DeploymentBuildType = "Metalama_MetalamaOpen_MetalamaOpenAutoCancellationToken_PublicDeployment",
                BumpBuildType = "Metalama_MetalamaOpen_MetalamaOpenAutoCancellationToken_VersionBump"
            };

        public static DependencyDefinition MetalamaOpenDependencyEmbedder { get; } =
            new( "Metalama.Open.DependencyEmbedder", VcsProvider.GitHub, "Metalama" )
            {
                // Metalama.Open.DependencyEmbedder is part of Metalama.Open products group, which is propagated to build types string.
                CiBuildTypes = new ConfigurationSpecific<string>(
                    "Metalama_MetalamaOpen_MetalamaOpenDependencyEmbedder_DebugBuild",
                    "Metalama_MetalamaOpen_MetalamaOpenDependencyEmbedder_ReleaseBuild",
                    "Metalama_MetalamaOpen_MetalamaOpenDependencyEmbedder_PublicBuild" ),
                DeploymentBuildType = "Metalama_MetalamaOpen_MetalamaOpenDependencyEmbedder_PublicDeployment",
                BumpBuildType = "Metalama_MetalamaOpen_MetalamaOpenDependencyEmbedder_VersionBump"
            };

        public static DependencyDefinition MetalamaOpenVirtuosity { get; } =
            new( "Metalama.Open.Virtuosity", VcsProvider.GitHub, "Metalama" )
            {
                // Metalama.Open.Virtuosity is part of Metalama.Open products group, which is propagated to build types string.
                CiBuildTypes = new ConfigurationSpecific<string>(
                    "Metalama_MetalamaOpen_MetalamaOpenVirtuosity_DebugBuild",
                    "Metalama_MetalamaOpen_MetalamaOpenVirtuosity_ReleaseBuild",
                    "Metalama_MetalamaOpen_MetalamaOpenVirtuosity_PublicBuild" ),
                DeploymentBuildType = "Metalama_MetalamaOpen_MetalamaOpenVirtuosity_PublicDeployment",
                BumpBuildType = "Metalama_MetalamaOpen_MetalamaOpenVirtuosity_VersionBump"
            };

        public static DependencyDefinition PostSharpEngineering { get; } = new(
            "PostSharp.Engineering",
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

        [Obsolete( "Renamed to MetalamaBackstage" )]
        public static DependencyDefinition PostSharpBackstageSettings { get; } = new(
            "PostSharp.Backstage.Settings",
            VcsProvider.AzureRepos,
            "Metalama" );

        public static DependencyDefinition MetalamaBackstage { get; } = new(
            "Metalama.Backstage",
            VcsProvider.AzureRepos,
            "Metalama" );

        public static ImmutableArray<DependencyDefinition> All { get; } = ImmutableArray.Create(
            MetalamaCompiler,
            Metalama,
            MetalamaDocumentation,
            MetalamaSamples,
            MetalamaTry,
            MetalamaVsx,
            MetalamaOpenAutoCancellationToken,
            MetalamaOpenDependencyEmbedder,
            MetalamaOpenVirtuosity,
            PostSharpEngineering,
            MetalamaBackstage );
    }
}