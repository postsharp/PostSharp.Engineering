using System;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class DependencyDefinition
    {
        public string Name { get; }

        public string NameWithoutDot => this.Name.Replace( ".", "", StringComparison.Ordinal );

        public string RepoName { get; init; }

        public string DefaultBranch { get; init; }

        public ConfigurationSpecific<string> CiBuildTypes { get; init; }
        
        public bool IsVersioned { get; }
        
        public string? BumpBuildType { get; init; }
        
        public string DeploymentBuildType { get; init; }

        public string VcsProjectName { get; }

        public VcsProvider Provider { get; }

        public bool GenerateSnapshotDependency { get; init; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyDefinition"/> class that represents an external dependency, i.e. one
        /// that we do not build ourselves.
        /// </summary>
        public DependencyDefinition( string name, VcsProvider provider, string vcsProjectName, bool isVersioned = true )
        {
            this.Name = name;
            this.Provider = provider;
            this.VcsProjectName = vcsProjectName;
            this.RepoName = name;
            this.DefaultBranch = "master";
            this.IsVersioned = isVersioned;

            this.CiBuildTypes = new ConfigurationSpecific<string>(
                $"{this.VcsProjectName}_{this.NameWithoutDot}_DebugBuild",
                $"{this.VcsProjectName}_{this.NameWithoutDot}_ReleaseBuild",
                $"{this.VcsProjectName}_{this.NameWithoutDot}_PublicBuild" );

            if ( this.IsVersioned )
            {
                this.BumpBuildType = $"{this.VcsProjectName}_{this.NameWithoutDot}_VersionBump";
            }
            
            this.DeploymentBuildType = $"{this.VcsProjectName}_{this.NameWithoutDot}_PublicDeployment";
        }

        public override string ToString() => this.Name;
    }
}