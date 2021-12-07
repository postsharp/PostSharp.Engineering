using PostSharp.Engineering.BuildTools.Build.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class DependencyDefinition
    {
        public string Name { get; }

        public string RepoName { get; init; }

        public string DefaultBranch { get; init; } = "master";

        public string VcsProjectName { get; }

        public string? CiBuildTypeId { get; }

        public VcsProvider Provider { get; }

        public string RestoredArtifactsDirectory { get; init; } = "artifacts/publish/private";

        public DependencyDefinition( string name, VcsProvider provider, string vcsProjectName, string? ciBuildTypeId = null )
        {
            this.Name = name;
            this.Provider = provider;
            this.VcsProjectName = vcsProjectName;
            this.RepoName = name;
            this.CiBuildTypeId = ciBuildTypeId;
        }
    }
}