// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class DependencyDefinition
    {
        public string Name { get; }

        public string NameWithoutDot => this.Name.Replace( ".", "", StringComparison.Ordinal );

        public string DefaultBranch { get; init; }

        public ConfigurationSpecific<string> CiBuildTypes { get; init; }

        public bool IsVersioned { get; }

        public string? BumpBuildType { get; init; }

        public string DeploymentBuildType { get; init; }

        public bool GenerateSnapshotDependency { get; init; } = true;

        public string EngineeringDirectory { get; init; } = "eng";

        public string CodeStyle { get; init; } = "Standard";

        public bool RequiresPublicVersionOnGitHub { get; init; } = true;

        public VcsRepo Repo { get; }

        public DependencyDefinition( string dependencyName, VcsProvider vcsProvider, string vcsProjectName, bool isVersioned = true )
        {
            this.Name = dependencyName;
            this.Repo = new VcsRepo( dependencyName, vcsProjectName, vcsProvider );
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