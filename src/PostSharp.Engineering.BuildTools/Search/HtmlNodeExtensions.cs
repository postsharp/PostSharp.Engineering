// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using HtmlAgilityPack;
using System;

namespace PostSharp.Engineering.BuildTools.Search;

internal static class HtmlNodeExtensions
{
    private static readonly string _zeroWidthSpace = ((char) 8203).ToString();

    public static string GetText( this HtmlNode node, bool isPreformatted = false )
    {
        var text = HtmlEntity.DeEntitize(
                node.InnerText
                    .Replace( "&ZeroWidthSpace;", "", StringComparison.OrdinalIgnoreCase ) )
            .Replace( _zeroWidthSpace, "", StringComparison.Ordinal );

        if ( !isPreformatted )
        {
            text = text.Trim();
        }

        return text;
    }
}