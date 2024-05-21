// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public static class BuildOrderHelper
{
    public static void SetBuildOrder( params DependencyDefinition[] orderedProducts )
    {
        for ( var i = 0; i < orderedProducts.Length; i++ )
        {
            orderedProducts[i].BuildOrder = i * 100;
        }
    }
}