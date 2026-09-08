// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Dependencies.Definitions;
using Xunit;

namespace PostSharp.Engineering.BuildTools.Tests;

/// <summary>
/// The PostSharp 2026.0 line receives its changes from the 2024.0 line through the upstream merge, and it declares two
/// definitions of one repository. Both arrangements are resolved by name at run time rather than by the compiler, so
/// they fail as a missing configuration or a type-initialization error rather than as a build break.
/// </summary>
public class PostSharpUpstreamTests
{
    [Fact]
    public void PostSharp20260_DeclaresPostSharp20240AsItsUpstream()
        => Assert.Same( PostSharpDependencies.V2024_0.Family, PostSharpDependencies.V2026_0.Family.UpstreamProductFamily );

    /// <summary>
    /// The upstream merge looks the upstream repository up by the product name of the downstream repository, so the
    /// two definitions must carry the same name. They do not have to, as far as the compiler is concerned.
    /// </summary>
    [Fact]
    public void UpstreamOfPostSharp20260_IsResolvedByTheProductName()
    {
        var productName = PostSharpDependencies.V2026_0.PostSharpProduct.Name;
        var upstreamFamily = PostSharpDependencies.V2026_0.Family.UpstreamProductFamily;

        Assert.NotNull( upstreamFamily );
        Assert.True( upstreamFamily.TryGetDependencyDefinition( productName, out var upstreamDefinition ) );
        Assert.Equal( "develop/2024.0", upstreamDefinition.Branch );
    }

    [Fact]
    public void BuildableDefinitions_UseTheDevelopmentAndReleaseBranches()
    {
        Assert.Equal( "develop/2026.0", PostSharpDependencies.V2026_0.PostSharpProduct.Branch );
        Assert.Equal( "release/2026.0", PostSharpDependencies.V2026_0.PostSharpProduct.ReleaseBranch );
        Assert.Equal( "develop/2024.0", PostSharpDependencies.V2024_0.PostSharpProduct.Branch );
        Assert.Equal( "release/2024.0", PostSharpDependencies.V2024_0.PostSharpProduct.ReleaseBranch );
    }

    /// <summary>
    /// The version bump configuration is generated only for a versioned definition, and the consuming definition is
    /// not versioned. Were the product built from the consuming one, no bump configuration would be generated at all.
    /// </summary>
    [Fact]
    public void BuildableDefinitionIsVersioned_AndTheConsumingOneIsNot()
    {
        Assert.True( PostSharpDependencies.V2026_0.PostSharpProduct.IsVersioned );
        Assert.False( PostSharpDependencies.V2026_0.PostSharp.IsVersioned );
    }

    /// <summary>
    /// The two definitions of the 2026.0 repository share one TeamCity project. The index that answers "which product
    /// is built here" must return the buildable one.
    /// </summary>
    [Fact]
    public void WhereTwoDefinitionsShareACiProject_TheBuildableOneIsIndexed()
    {
        var product = PostSharpDependencies.V2026_0.PostSharpProduct;
        var package = PostSharpDependencies.V2026_0.PostSharp;

        Assert.Equal( package.CiConfiguration.ProjectId.Id, product.CiConfiguration.ProjectId.Id );

        Assert.True(
            PostSharpDependencies.V2026_0.Family.TryGetDependencyDefinitionByCiId(
                product.CiConfiguration.ProjectId.Id,
                out var indexedDefinition ) );

        Assert.Same( product, indexedDefinition );
    }

    /// <summary>
    /// The name of the consuming definition is what makes the version property <c>PostSharpPackageVersion</c>, which
    /// Metalama.Vsx references by that name in its <c>Directory.Packages.props</c>.
    /// </summary>
    [Fact]
    public void ConsumingDefinition_KeepsItsName()
        => Assert.Equal( "PostSharpPackage", PostSharpDependencies.V2026_0.PostSharp.Name );

    /// <summary>
    /// The engineering directory of the buildable definition is where the version files are read from. The consuming
    /// definition points at the directory the published distribution carries instead.
    /// </summary>
    [Fact]
    public void BuildableDefinition_ReadsItsVersionFromTheDefaultEngineeringDirectory()
    {
        Assert.Equal( "eng", PostSharpDependencies.V2026_0.PostSharpProduct.EngineeringDirectory );
        Assert.Equal( @"Build\Distribution\eng", PostSharpDependencies.V2026_0.PostSharp.EngineeringDirectory );
    }
}
