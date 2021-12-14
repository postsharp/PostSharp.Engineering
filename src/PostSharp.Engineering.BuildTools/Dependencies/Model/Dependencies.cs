using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public static class Dependencies
    {
        public static DependencyDefinition Roslyn { get; } = new(
            "Roslyn",
            VcsProvider.GitHub,
            "Roslyn" );

        public static DependencyDefinition MetalamaCompiler { get; } = new(
            "Metalama.Compiler",
            VcsProvider.AzureRepos,
            "Metalama",
            "Metalama_MetalamaCompiler_ReleaseBuild" );

        public static DependencyDefinition Metalama { get; } = new( "Metalama", VcsProvider.AzureRepos, "Metalama", "Metalama_Metalama_DebugBuild" );

        public static DependencyDefinition MetalamaSamples { get; } = new( "Metalama.Samples", VcsProvider.GitHub, "postsharp" );

        public static DependencyDefinition MetalamaDocumentation { get; } = new( "Metalama.Documentation", VcsProvider.GitHub, "postsharp" );

        public static DependencyDefinition MetalamaTry { get; } = new( "Metalama.Try", VcsProvider.AzureRepos, "Metalama" );

        public static DependencyDefinition PostSharpEngineering { get; } = new(
            "PostSharp.Engineering",
            VcsProvider.GitHub,
            "postsharp",
            "PostSharpEngineering_DebugBuild" );

        public static DependencyDefinition PostSharpBackstageSettings { get; } = new(
            "PostSharp.Backstage.Settings",
            VcsProvider.AzureRepos,
            "Metalama",
            "Metalama_PostSharpBackstageSettings_DebugBuild" );

        public static ImmutableArray<DependencyDefinition> All { get; } = ImmutableArray.Create(
            MetalamaCompiler,
            Metalama,
            MetalamaDocumentation,
            MetalamaSamples,
            MetalamaTry,
            PostSharpEngineering,
            PostSharpBackstageSettings );
    }
}