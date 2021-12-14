namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class DependencyDefinition
    {
        public string Name { get; }

        public string RepoName { get; init; }

        public string DefaultBranch { get; init; } = "master";

        public string? DefaultCiBuildTypeId { get; }

        public string VcsProjectName { get; }

        public VcsProvider Provider { get; }

        public DependencyDefinition( string name, VcsProvider provider, string vcsProjectName, string? ciBuildTypeId = null )
        {
            this.Name = name;
            this.Provider = provider;
            this.VcsProjectName = vcsProjectName;
            this.RepoName = name;
            this.DefaultCiBuildTypeId = ciBuildTypeId;
        }
    }
}