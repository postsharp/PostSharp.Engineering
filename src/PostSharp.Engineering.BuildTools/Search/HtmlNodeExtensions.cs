// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using HtmlAgilityPack;
using System;

namespace PostSharp.Engineering.BuildTools.Search;

internal static class HtmlNodeExtensions
{
    private static readonly string _zeroWidthSpace = ((char) 8203).ToString();
    private static readonly string _softHyphen = ((char) 173).ToString();

    public static string GetText( this HtmlNode node, bool isPreformatted = false )
    {
        var text = HtmlEntity.DeEntitize(
                node.InnerText
                    .Replace( "&ZeroWidthSpace;", "", StringComparison.OrdinalIgnoreCase )
                    .Replace( "&shy;", "", StringComparison.OrdinalIgnoreCase ) )
            .Replace( _zeroWidthSpace, "", StringComparison.Ordinal )
            .Replace( _softHyphen, "", StringComparison.Ordinal );

        if ( !isPreformatted )
        {
            text = text.Trim();
        }

        return text;
    }
}