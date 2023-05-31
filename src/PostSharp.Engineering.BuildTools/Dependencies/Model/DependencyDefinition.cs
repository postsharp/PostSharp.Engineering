// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
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
        public string Branch { get; }

        /// <summary>
        /// Gets the release branch for this product.
        /// </summary>
        /// <remarks>
        /// The release branch is the branch containing public code of the latest published version of the product.
        /// The release branch is not set for products not having their source code published.
        /// </remarks>
        public string? ReleaseBranch { get; }

        public CiConfiguration CiConfiguration { get; }

        public bool IsVersioned { get; }

        public bool GenerateSnapshotDependency { get; init; } = true;

        public string EngineeringDirectory { get; init; } = "eng";

        public string CodeStyle { get; init; } = "Standard";

        public VcsRepo Repo { get; }

        public ParametricString PrivateArtifactsDirectory { get; init; } = Path.Combine( "artifacts", "publish", "private" );

        public DependencyDefinition(
            ProductFamily productFamily,
            string dependencyName,
            string branch,
            string? releaseBranch,
            VcsProvider vcsProvider,
            string vcsProjectName,
            ICiConfigurationFactory? ciConfigurationFactory,
            bool isVersioned = true )
        {
            this.ProductFamily = productFamily;

            this.Name = dependencyName;
            this.Repo = new VcsRepo( dependencyName, vcsProjectName, vcsProvider );
            this.Branch = branch;
            this.ReleaseBranch = releaseBranch;
            this.IsVersioned = isVersioned;
            
            this.CiConfiguration = ciConfigurationFactory.Create( this.ProductFamily, this.NameWithoutDot, this.IsVersioned );

            productFamily.Register( this );
        }

        public override string ToString() => this.Name;
    }
}