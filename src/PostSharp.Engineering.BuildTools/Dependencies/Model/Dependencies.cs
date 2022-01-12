using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public static class Dependencies
    {
        public static DependencyDefinition Roslyn { get; } = new(
            "Roslyn",
            VcsProvider.None,
            "Roslyn" );

        public static DependencyDefinition MetalamaCompiler { get; } = new(
            "Metalama.Compiler",
            VcsProvider.AzureRepos,
            "Metalama",

            // The release build is intentionally used for the debug configuration because we want dependencies to consume the release
            // build, for performance reasons. The debug build will be used only locally, and for this we don't need a configuration here.
            ("Metalama_MetalamaCompiler_ReleaseBuild", "Metalama_MetalamaCompiler_ReleaseBuild", "Metalama_MetalamaCompiler_PublicBuild") );

        public static DependencyDefinition Metalama { get; } = new(
            "Metalama",
            VcsProvider.AzureRepos,
            "Metalama",
            ("Metalama_Metalama_DebugBuild", "Metalama_Metalama_ReleaseBuild", "Metalama_Metalama_PublicBuild") );

        public static DependencyDefinition MetalamaSamples { get; } = new( "Metalama.Samples", VcsProvider.GitHub, "postsharp" );

        public static DependencyDefinition MetalamaDocumentation { get; } = new( "Metalama.Documentation", VcsProvider.GitHub, "postsharp" );

        public static DependencyDefinition MetalamaTry { get; } = new( "Metalama.Try", VcsProvider.AzureRepos, "Metalama" );

        public static DependencyDefinition PostSharpEngineering { get; } = new(
            "PostSharp.Engineering",
            VcsProvider.GitHub,
            "postsharp",

            // We always use the debug build for engineering.
            ("PostSharpEngineering_DebugBuild", "PostSharpEngineering_DebugBuild", "PostSharpEngineering_DebugBuild") ) { GenerateSnapshotDependency = false };

        public static DependencyDefinition PostSharpBackstageSettings { get; } = new(
            "PostSharp.Backstage.Settings",
            VcsProvider.AzureRepos,
            "Metalama",
            ("Metalama_PostSharpBackstageSettings_DebugBuild", "Metalama_PostSharpBackstageSettings_ReleaseBuild",
             "Metalama_PostSharpBackstageSettings_PublicBuild") );

        public static ImmutableArray<DependencyDefinition> All { get; } = ImmutableArray.Create(
            Roslyn,
            MetalamaCompiler,
            Metalama,
            MetalamaDocumentation,
            MetalamaSamples,
            MetalamaTry,
            PostSharpEngineering,
            PostSharpBackstageSettings );
    }
}