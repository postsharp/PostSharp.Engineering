// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools;
using PostSharp.Engineering.BuildTools.Build.Model;

namespace PostSharp.Engineering.DocFx;

internal class DocFxCommandData( Product product, DocFxOptions options ) : BaseCommandData( product )
{
    public DocFxOptions Options { get; } = options;
}