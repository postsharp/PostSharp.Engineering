// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using HtmlAgilityPack;
using System;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Search.Crawlers;

public class PostSharpDocCrawler : DocFxCrawler
{
    protected override BreadcrumbInfo GetBreadcrumbData( HtmlNode[] breadcrumbLinks )
    {
        var relevantBreadCrumbTitles = breadcrumbLinks
            .Skip( 4 )
            .Select( n => n.GetText() )
            .ToArray(); 
        
        var breadcrumb = string.Join(
            " > ",
            relevantBreadCrumbTitles );

        var isDefaultKind = breadcrumbLinks.Length < 5;

        var category = breadcrumbLinks.Length < 5
            ? null
            : NormalizeCategoryName( breadcrumbLinks.Skip( 4 ).First().GetText() );

        var isApiReference = category?.Equals( "api reference", StringComparison.OrdinalIgnoreCase ) ?? false;

        var kind = isDefaultKind
            ? "General Information"
            : isApiReference
                ? "API Documentation"
                : "Conceptual Documentation";

        int kindRank;
        Func<HtmlNode, bool> isNextParagraphIgnored = _ => false;
        
        if ( isDefaultKind )
        {
            kindRank = (int) DocFxKindRank.Common;
        }
        else if ( isApiReference )
        {
            isNextParagraphIgnored = DocFxApiArticleHelper.IsNextParagraphIgnored;
            kindRank = (int) DocFxKindRank.Api;
        }
        else
        {
            kindRank = (int) DocFxKindRank.Conceptual;
        }

        return new(
            breadcrumb,
            new[] { kind },
            kindRank,
            category == null ? Array.Empty<string>() : new[] { category },
            relevantBreadCrumbTitles.Length,
            false,
            isNextParagraphIgnored );
    }
}