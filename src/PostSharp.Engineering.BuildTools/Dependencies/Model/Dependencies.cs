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

        public static DependencyDefinition MetalamaSamples { get; } = new( "Metalama.Samples", VcsProvider.GitHub, "postsharp", false );

        public static DependencyDefinition MetalamaDocumentation { get; } = new( "Metalama.Documentation", VcsProvider.GitHub, "postsharp", false );

        public static DependencyDefinition MetalamaTry { get; } = new( "Metalama.Try", VcsProvider.AzureRepos, "Metalama", false );

        public static DependencyDefinition MetalamaVsx { get; } = new( "Metalama.Vsx", VcsProvider.AzureRepos, "Metalama" );

        public static DependencyDefinition PostSharpEngineering { get; } = new(
            "PostSharp.Engineering",
            VcsProvider.GitHub,
            "postsharp" )
        {
            GenerateSnapshotDependency = false,

            // We always use the debug build for engineering.
            CiBuildTypes = new ConfigurationSpecific<string>(
                "PostSharpEngineering_DebugBuild",
                "PostSharpEngineering_ReleaseBuild",
                "PostSharpEngineering_PublicBuild" )
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
            PostSharpEngineering,
            MetalamaBackstage );
    }
}