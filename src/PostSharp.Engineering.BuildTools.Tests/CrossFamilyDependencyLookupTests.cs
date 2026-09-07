// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Dependencies.Definitions;
using Xunit;

namespace PostSharp.Engineering.BuildTools.Tests;

/// <summary>
/// Contains tests that cover the resolution of a transitive dependency reached through a direct dependency of another
/// product family. BusinessSystems, in the Business Systems family, depends on Metalama 2027.0; the restored version
/// file of Metalama lists Metalama.Compiler, which the Business Systems family does not define.
/// </summary>
public class CrossFamilyDependencyLookupTests
{
    [Fact]
    public void ProductFamilyAlone_DoesNotKnowTheTransitiveDependencyOfAnotherFamily()
    {
        // The reason the product-level lookup exists: the family of the consumer cannot resolve the name.
        Assert.False( BusinessSystemsDependencies.Family.TryGetDependencyDefinition( "Metalama.Compiler", out _ ) );
    }

    [Fact]
    public void Product_ResolvesTheTransitiveDependency_ThroughTheFamilyOfItsDirectDependency()
    {
        var product = new Product( BusinessSystemsDependencies.BusinessSystems );

        Assert.True( product.TryGetDependencyDefinition( "Metalama.Compiler", out var definition ) );
        Assert.Same( MetalamaDependencies.V2027_0.MetalamaCompiler, definition );
        Assert.Same( definition, product.GetDependencyDefinition( "Metalama.Compiler" ) );
    }

    [Fact]
    public void Product_StillResolvesItsDirectDependencies_AndItsOwnFamily()
    {
        var product = new Product( BusinessSystemsDependencies.BusinessSystems );

        Assert.True( product.TryGetDependencyDefinition( "Metalama", out var metalama ) );
        Assert.Same( MetalamaDependencies.V2027_0.Metalama, metalama );

        Assert.True( product.TryGetDependencyDefinition( "HelpBrowser", out var helpBrowser ) );
        Assert.Same( BusinessSystemsDependencies.HelpBrowser, helpBrowser );

        Assert.False( product.TryGetDependencyDefinition( "NoSuchDependency", out _ ) );
    }
}
