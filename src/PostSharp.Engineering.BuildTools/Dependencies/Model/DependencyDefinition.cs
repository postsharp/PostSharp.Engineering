// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using System;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class DependencyDefinition
    {
        public ProductFamily ProductFamily { get; }

        public string Name { get; }

        public string NameWithoutDot => this.Name.Replace( ".", "", StringComparison.Ordinal );

        /// <summary>
        /// Gets the development branch for this product.
        /// </summary>
        /// <remarks>
        /// The development branch is the target branch for all topic and feature branches. 
        /// </remarks>
        public string Branch { get; init; }

        /// <summary>
        /// Gets the release branch for this product.
        /// </summary>
        /// <remarks>
        /// The release branch is the branch containing public code of the latest published version of the product.
        /// The release branch is not set for products not having their source code published.
        /// </remarks>
        public string? ReleaseBranch { get; init; }

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

        public DependencyDefinition(
            ProductFamily productFamily,
            string dependencyName,
            string branch,
            string? releaseBranch,
            VcsProvider vcsProvider,
            string vcsProjectName,
            bool isVersioned = true,
            bool includeVersionInCiProjectName = true )
        {
            this.ProductFamily = productFamily;

            this.Name = dependencyName;
            this.Repo = new VcsRepo( dependencyName, vcsProjectName, vcsProvider );
            this.Branch = branch;
            this.IsVersioned = isVersioned;
            var ciProjectName = vcsProjectName.Replace( ".", "_", StringComparison.Ordinal );

            if ( includeVersionInCiProjectName )
            {
                ciProjectName = $"{ciProjectName}_{ciProjectName}{MainVersion.ValueWithoutDots}";
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

            productFamily.Register( this );
        }

        public override string ToString() => this.Name;
    }
}