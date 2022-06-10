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

        /// </summary>
        public DependencyDefinition( string name, VcsProvider provider, string vcsProjectName, bool isVersioned = true )
        {
            this.Name = name;
            this.Provider = provider;
            this.VcsProjectName = vcsProjectName;
            this.RepoName = name;
            this.DefaultBranch = "master";
            this.IsVersioned = isVersioned;
            var vcsProjectNameWithUnderscore = vcsProjectName.Replace( ".", "_", StringComparison.Ordinal );

            this.CiBuildTypes = new ConfigurationSpecific<string>(
                $"{vcsProjectNameWithUnderscore}_{this.NameWithoutDot}_DebugBuild",
                $"{vcsProjectNameWithUnderscore}_{this.NameWithoutDot}_ReleaseBuild",
                $"{vcsProjectNameWithUnderscore}_{this.NameWithoutDot}_PublicBuild" );

            if ( this.IsVersioned )
            {
                this.BumpBuildType = $"{vcsProjectNameWithUnderscore}_{this.NameWithoutDot}_VersionBump";
            }

            this.DeploymentBuildType = $"{vcsProjectNameWithUnderscore}_{this.NameWithoutDot}_PublicDeployment";
        }

        public override string ToString() => this.Name;
    }
}