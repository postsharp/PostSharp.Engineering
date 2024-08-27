// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Markdig;

namespace PostSharp.Engineering.DocFx;

public class DocFxOptions
{
    public Action<MarkdownPipelineBuilder>? ConfigureMarkdig { get; init; }
}