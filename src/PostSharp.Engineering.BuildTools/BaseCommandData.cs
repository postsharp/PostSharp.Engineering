// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;

namespace PostSharp.Engineering.BuildTools;

public class BaseCommandData( Product product )
{
    public Product Product { get; } = product;
}