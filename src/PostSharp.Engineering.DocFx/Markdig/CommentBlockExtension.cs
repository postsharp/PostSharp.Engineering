// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Markdig;
using Markdig.Renderers;

namespace PostSharp.Engineering.DocFx.Markdig;

public class CommentBlockExtension : IMarkdownExtension
{
    public void Setup( MarkdownPipelineBuilder pipeline )
    {
        if ( !pipeline.BlockParsers.Contains<CommentBlockParser>() )
        {
            pipeline.BlockParsers.Insert( 0, new CommentBlockParser() );
        }
    }

    public void Setup( MarkdownPipeline pipeline, IMarkdownRenderer renderer ) { }
}