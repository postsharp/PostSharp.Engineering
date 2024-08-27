// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Docfx.MarkdigEngine.Extensions;
using Markdig.Parsers;

namespace PostSharp.Engineering.DocFx.Markdig;

// Markdig doesn't always hide the comment block, so we do it ourselves.
public class CommentBlockParser : BlockParser
{
    public override BlockState TryOpen( BlockProcessor processor )
    {
        var slice = processor.Line;

        if ( processor.IsCodeIndent
             || !ExtensionsHelper.MatchStart( ref slice, "[comment]" ) )
        {
            return BlockState.None;
        }
        else
        {
            return BlockState.BreakDiscard;
        }
    }
}