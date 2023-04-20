// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using System;
using System.IO;

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

        public VcsRepo Repo { get; }

        public ParametricString PrivateArtifactsDirectory { get; init; } = Path.Combine( "artifacts", "publish", "private" );

        public string VcsConfigName { get; init; }

        public DependencyDefinition( string dependencyName, VcsProvider vcsProvider, string vcsProjectName, bool isVersioned = true, bool includeVersionInCiProjectName = true )
        {
            this.Name = dependencyName;
            this.Repo = new VcsRepo( dependencyName, vcsProjectName, vcsProvider );
            this.DefaultBranch = vcsProvider.DefaultBranch;
            this.IsVersioned = isVersioned;
            var ciProjectName = vcsProjectName.Replace( ".", "_", StringComparison.Ordinal );

            if ( includeVersionInCiProjectName )
            {
                ciProjectName += MainVersion.Value.Replace( ".", "", StringComparison.Ordinal );
            }

            this.CiBuildTypes = new ConfigurationSpecific<string>(
                $"{ciProjectName}_{this.NameWithoutDot}_DebugBuild",
                $"{ciProjectName}_{this.NameWithoutDot}_ReleaseBuild",
                $"{ciProjectName}_{this.NameWithoutDot}_PublicBuild" );

            if ( this.IsVersioned )
            {
                this.BumpBuildType = $"{ciProjectName}_{this.NameWithoutDot}_VersionBump";
            }

            this.DeploymentBuildType = $"{ciProjectName}_{this.NameWithoutDot}_PublicDeployment";
            this.VcsConfigName = $"{ciProjectName}_{this.NameWithoutDot}";
        }

        public override string ToString() => this.Name;
    }
}