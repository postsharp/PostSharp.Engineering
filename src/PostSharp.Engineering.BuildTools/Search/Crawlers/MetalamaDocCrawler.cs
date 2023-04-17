// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using HtmlAgilityPack;
using System;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Search.Crawlers;

public class MetalamaDocCrawler : DocFxCrawler
{
    protected override BreadcrumbInfo GetBreadcrumbData( HtmlNode[] breadcrumbLinks )
    {
        var isDefaultKind = breadcrumbLinks.Length < 5;

        var kind = isDefaultKind
            ? "General Information"
            : NormalizeCategoryName( breadcrumbLinks.Skip( 4 ).First().GetText() );

        var breadcrumbTitlesCountToSkip = 6;
        var hasCategory = true;
        
        int kindRank;
        
        Func<HtmlNode, bool> isNextParagraphIgnored = n => false;

        if ( isDefaultKind )
        {
            kindRank = (int) DocFxKindRank.Common;
        }
        else if ( kind.Contains( "example", StringComparison.OrdinalIgnoreCase ) )
        {
            breadcrumbTitlesCountToSkip = 5;
            hasCategory = false;
            kindRank = (int) DocFxKindRank.Examples;
        }
        else if ( kind.Contains( "concept", StringComparison.OrdinalIgnoreCase ) )
        {
            kindRank = (int) DocFxKindRank.Conceptual;
        }
        else if ( kind.Contains( "api", StringComparison.OrdinalIgnoreCase ) )
        {
            isNextParagraphIgnored = DocFxApiArticleHelper.IsNextParagraphIgnored;
            kindRank = (int) DocFxKindRank.Api;
        }
        else
        {
            kindRank = (int) DocFxKindRank.Unknown;
        }
        
        var relevantBreadCrumbTitles = breadcrumbLinks
            .Skip( breadcrumbTitlesCountToSkip )
            .Select( n => n.GetText() )
            .ToArray();

        var breadcrumb = string.Join(
            " > ",
            relevantBreadCrumbTitles );

        var category = !hasCategory || breadcrumbLinks.Length < 6
            ? null
            : NormalizeCategoryName( breadcrumbLinks.Skip( 5 ).First().GetText() );

        return new( breadcrumb, new[] { kind }, kindRank, category == null ? Array.Empty<string>() : new[] { category }, relevantBreadCrumbTitles.Length, isNextParagraphIgnored );
    }
}