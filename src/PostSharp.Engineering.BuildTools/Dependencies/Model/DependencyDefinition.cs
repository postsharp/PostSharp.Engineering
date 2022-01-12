using System;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class DependencyDefinition
    {
        public string Name { get; }

        public string NameWithoutDot => this.Name.Replace( ".", "", StringComparison.Ordinal );

        public string RepoName { get; init; }

        public string DefaultBranch { get; }

        public ConfigurationSpecific<string> CiBuildTypes { get; }

        public string VcsProjectName { get; }

        public VcsProvider Provider { get; }

        public bool GenerateSnapshotDependency { get; init; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyDefinition"/> class that represents an external dependency, i.e. one
        /// that we do not build ourselves.
        /// </summary>
        public DependencyDefinition( string name, VcsProvider provider, string vcsProjectName )
        {
            this.Name = name;
            this.Provider = provider;
            this.VcsProjectName = vcsProjectName;
            this.RepoName = name;
            this.DefaultBranch = "master";
            this.CiBuildTypes = new ConfigurationSpecific<string>( "N/A", "N/A", "N/A" );
        }

        public DependencyDefinition(
            string name,
            VcsProvider provider,
            string vcsProjectName,
            in (string Debug, string Release, string Public) ciBuildTypes,
            string defaultBranch = "master" )
        {
            this.Name = name;
            this.Provider = provider;
            this.VcsProjectName = vcsProjectName;
            this.RepoName = name;
            this.DefaultBranch = defaultBranch;
            this.CiBuildTypes = new ConfigurationSpecific<string>( ciBuildTypes );
        }

        public override string ToString() => this.Name;
    }
}