// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    // ReSharper disable once InconsistentNaming

    /// <summary>
    /// Information about a build, required to format a <see cref="ParametricString"/>.
    /// </summary>
    /// <param name="PackageVersion">Full NuGet package version.</param>
    /// <param name="Configuration">Configuration name.</param>
    /// <param name="MSBuildConfiguration">MSBuild configuration name.</param>
    public record BuildInfo( string? PackageVersion, string Configuration, string MSBuildConfiguration, string? PackagePreviewVersion )
    {
        public BuildInfo( string? packageVersion, BuildConfiguration configuration, Product product, string? packagePreviewVersion ) : this(
            packageVersion,
            configuration.ToString(),
            product.DependencyDefinition.MSBuildConfiguration[configuration],
            packagePreviewVersion ) { }

        public bool IsPrerelease => this.PackageVersion?.Contains( "-", StringComparison.Ordinal ) ?? throw new InvalidOperationException();
    }
}