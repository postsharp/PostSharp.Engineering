using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public static class Dependencies
    {
        public static DependencyDefinition CaravelaCompiler { get; } = new( "Caravela.Compiler", VcsProvider.AzureRepos, "Caravela", "Caravela_CaravelaCompiler" )
        {
            RestoredArtifactsDirectory = "artifacts/packages/release/Shipping"
        };

        public static DependencyDefinition Caravela { get; } = new( "Caravela", VcsProvider.AzureRepos, "Caravela", "Caravela_Caravela" );

        public static DependencyDefinition CaravelaSamples { get; } = new( "Caravela.Samples", VcsProvider.GitHub, "postsharp" );

        public static DependencyDefinition CaravelaDocumentation { get; } = new( "Caravela.Documentation", VcsProvider.GitHub, "postsharp" );

        public static DependencyDefinition CaravelaTry { get; } = new( "Caravela.Try", VcsProvider.AzureRepos, "Caravela" );

        public static DependencyDefinition PostSharpEngineering { get; } = new(
            "PostSharp.Engineering",
            VcsProvider.GitHub,
            "postsharp",
            "PostSharpEngineering_DebugBuild" );

        public static DependencyDefinition PostSharpBackstageSettings { get; } = new DependencyDefinition(
            "PostSharp.Backstage.Settings",
            VcsProvider.AzureRepos,
            "Caravela",
            "Caravela_PostSharpBackstageSettings_DebugBuildAndTest" );

        public static ImmutableArray<DependencyDefinition> All { get; } = ImmutableArray.Create(
            CaravelaCompiler,
            Caravela,
            CaravelaDocumentation,
            CaravelaSamples,
            CaravelaTry,
            PostSharpEngineering,
            PostSharpBackstageSettings );
    }
}