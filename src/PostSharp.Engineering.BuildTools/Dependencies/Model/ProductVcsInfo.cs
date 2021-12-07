using PostSharp.Engineering.BuildTools.Build.Model;
using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class ProductVcsInfo
    {
        public string Name { get; }

        public string RepoName { get; init; }

        public string DefaultBranch { get; init; } = "master";

        public string VcsProjectName { get; }

        public string? CiBuildTypeId { get; }

        public VcsProvider Provider { get; }

        public string RestoredArtifactsDirectory { get; init; } = "artifacts/publish/private";

        public ProductVcsInfo( string name, VcsProvider provider, string vcsProjectName, string? ciBuildTypeId = null )
        {
            this.Name = name;
            this.Provider = provider;
            this.VcsProjectName = vcsProjectName;
            this.RepoName = name;
            this.CiBuildTypeId = ciBuildTypeId;
        }

        public static ProductVcsInfo CaravelaCompiler { get; } = new( "Caravela.Compiler", VcsProvider.AzureRepos, "Caravela", "Caravela_CaravelaCompiler" )
        {
            RestoredArtifactsDirectory = "artifacts/packages/release/Shipping"
        };

        public static ProductVcsInfo Caravela { get; } = new( "Caravela", VcsProvider.AzureRepos, "Caravela", "Caravela_Caravela" );

        public static ProductVcsInfo CaravelaSamples { get; } = new( "Caravela.Samples", VcsProvider.GitHub, "postsharp" );

        public static ProductVcsInfo CaravelaDocumentation { get; } = new( "Caravela.Documentation", VcsProvider.GitHub, "postsharp" );

        public static ProductVcsInfo CaravelaTry { get; } = new( "Caravela.Try", VcsProvider.AzureRepos, "Caravela" );

        public static ProductVcsInfo PostSharpEngineering { get; } = new(
            "PostSharp.Engineering",
            VcsProvider.GitHub,
            "postsharp",
            "PostSharpEngineering_DebugBuild" );

        public static ImmutableArray<ProductVcsInfo> All { get; } = ImmutableArray.Create(
            CaravelaCompiler,
            Caravela,
            CaravelaDocumentation,
            CaravelaSamples,
            CaravelaTry,
            PostSharpEngineering );
    }
}