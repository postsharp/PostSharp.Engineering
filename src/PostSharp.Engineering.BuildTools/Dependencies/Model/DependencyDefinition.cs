// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    /// <summary>
    /// Represents the definition of a dependency. Dependencies are typically defined in PostSharp.Engineering. Dependency definitions
    /// must not define any property that depends on the referencing product. Any such property must be defined in <see cref="ParametrizedDependency"/>.
    /// </summary>
    public class DependencyDefinition
    {
        [return: NotNullIfNotNull( "dependency" )]
        public static implicit operator DependencyDefinition?( ParametrizedDependency? dependency ) => dependency?.Definition;

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

        public CiProjectConfiguration CiConfiguration { get; }

        public bool IsVersioned { get; init; } = true;

        public bool GenerateSnapshotDependency { get; init; } = true;

        public string EngineeringDirectory { get; init; } = "eng";

        public string CodeStyle { get; init; } = "Standard";

        public VcsRepository VcsRepository { get; }

        public ParametricString PrivateArtifactsDirectory { get; init; } = Path.Combine( "artifacts", "publish", "private" );

        public string GetResolvedPrivateArtifactsDirectory( BuildConfiguration configuration )
            => this.PrivateArtifactsDirectory.ToString(
                new BuildInfo( null, configuration.ToString().ToLowerInvariant(), this.MSBuildConfiguration[configuration] ) );

        public ConfigurationSpecific<string> MSBuildConfiguration { get; init; } = new( "Debug", "Release", "Release" );

        public ParametrizedDependency ToDependency() => new( this );

        public ParametrizedDependency ToDependency( ConfigurationSpecific<BuildConfiguration> configurationMapping )
            => new( this ) { ConfigurationMapping = configurationMapping };

        public DependencyDefinition(
            ProductFamily productFamily,
            string dependencyName,
            string branch,
            string? releaseBranch,
            VcsRepository vcsRepository,
            CiProjectConfiguration ciProjectConfiguration,
            bool isVersioned = true )
        {
            this.ProductFamily = productFamily;

            this.Name = dependencyName;
            this.VcsRepository = vcsRepository;
            this.Branch = branch;
            this.ReleaseBranch = releaseBranch;
            this.CiConfiguration = ciProjectConfiguration;
            this.IsVersioned = isVersioned;

            productFamily.Register( this );
        }

        public override string ToString() => this.Name;
    }
}