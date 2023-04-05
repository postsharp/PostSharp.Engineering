// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PostSharp.Engineering.BuildTools.Search.Crawlers;

public abstract class DocFxCrawler
{
    private static readonly char _newLineCharacter = Environment.NewLine[^1];
    private bool _isTextPreformatted;
    private readonly Dictionary<HtmlNode, int> _nodeLevels = new();
    private readonly Dictionary<HtmlNode, string?> _anchors = new();
    private readonly Headings _headings = new Headings();
    private string? _anchor;
    private readonly StringBuilder _text = new StringBuilder();

    public async IAsyncEnumerable<Snippet> GetSnippetsFromDocument(
        HtmlDocument document,
        string url,
        string source,
        string[] products )
    {
        var breadcrumbLinks = document.DocumentNode
            .SelectSingleNode( "//div[@id=\"breadcrum\"]" )
            .SelectNodes( ".//a|.//span[@class=\"current\"]" )
            .ToArray();

        var breadcrumb = this.GetBreadcrumbData( breadcrumbLinks );

        var complexityLevelNode = document.DocumentNode.SelectSingleNode( "//meta[@name=\"postsharp:level\"]" );
        var complexityLevelString = complexityLevelNode?.Attributes["content"]?.Value;

        var complexityLevel = string.IsNullOrEmpty( complexityLevelString )
            ? 0
            : int.Parse( complexityLevelString, CultureInfo.InvariantCulture );

        var complexityLeveRank = 1000 - complexityLevel - 1;

        await foreach ( var snippet in this.CrawlAsync(
                           document.DocumentNode
                               .SelectSingleNode( "//div[@id=\"content\"]" ),
                           new(
                               breadcrumb.Data.Breadcrumb,
                               url,
                               breadcrumb.Data.Kinds,
                               breadcrumb.Data.KindRank,
                               breadcrumb.Data.Categories,
                               source,
                               products,
                               complexityLevel,
                               complexityLeveRank ) ) )
        {
            if ( snippet != null )
            {
                yield return snippet;
            }
        }
    }

    protected abstract (BreadcrumbInfo Data, Func<HtmlNode, HtmlNode?> GetRootNode) GetBreadcrumbData( HtmlNode[] breadcrumbLinks );

    private async IAsyncEnumerable<Snippet?> CrawlAsync( HtmlNode node, ContentInfo contentInfo )
    {
        await foreach ( var snippet in this.CrawlRecursivelyAsync( node, null, contentInfo, true ) )
        {
            yield return snippet;
        }

        yield return this.CreateSnippet( contentInfo );
    }

    private async IAsyncEnumerable<Snippet?> CrawlRecursivelyAsync( HtmlNode node, HtmlNode? parentNode, ContentInfo contentInfo, bool skipNextSibling = false )
    {
        await Task.Yield();

        var skipChildNodes = false;
        var nodeName = node.Name.ToLowerInvariant();

        if ( Regex.IsMatch( nodeName, @"^h\d$" ) )
        {
            yield return this.CreateSnippet( contentInfo );

            var level = node.Name[1] - '0';

            if ( parentNode != null )
            {
                this._nodeLevels[parentNode] = level;
                this._anchors[parentNode] = this._anchor;
            }

            this._headings.Set( level, this.GetNodeText( node ) );
            this._anchor = node.Attributes["id"]?.Value;
            this._text.Clear();

            skipChildNodes = true;
        }
        else if ( node.NodeType == HtmlNodeType.Text )
        {
            var innerText = this.GetNodeText( node );

            if ( innerText != "" )
            {
                if ( this._text.Length > 0 && char.IsLetterOrDigit( innerText[0] ) && !char.IsWhiteSpace( this._text[^1] ) )
                {
                    this._text.Append( ' ' );
                }

                this._text.Append( innerText );
            }
        }
        else
        {
            switch ( nodeName )
            {
                case "div":
                    {
                        var cssClass = node.Attributes["class"]?.Value ?? "";

                        // Ignore code links in examples (See on GitHub, open in sandbox, ...)
                        if ( cssClass.Contains( "project-buttons", StringComparison.OrdinalIgnoreCase )

                             // Ignore code links (Open in sandbox, See on GitHub, ...)
                             || cssClass.Contains( "sample-links", StringComparison.OrdinalIgnoreCase )

                             // Ignore tabbed code
                             || cssClass.Contains( "tabGroup", StringComparison.OrdinalIgnoreCase )

                             // Ignore diff code in examples
                             || cssClass.Contains( "compare", StringComparison.OrdinalIgnoreCase ) )
                        {
                            skipChildNodes = true;
                        }

                        break;
                    }

                // Ignore code
                case "code":
                    skipChildNodes = true;

                    break;

                case "li":
                    this._text.Append( "- " );

                    break;

                case "pre":
                    this._isTextPreformatted = true;

                    break;

                case "table":
                    foreach ( var snippet in this.CrawlTable( node, contentInfo ) )
                    {
                        yield return snippet;
                    }

                    skipChildNodes = true;

                    break;
            }
        }

        if ( !skipChildNodes && node.HasChildNodes )
        {
            await foreach ( var snippet in this.CrawlRecursivelyAsync( node.FirstChild, node, contentInfo ) )
            {
                yield return snippet;
            }
        }

        switch ( nodeName )
        {
            case "br":
                this._text.AppendLine();

                break;

            // TODO: We could improve this by figuring out if the node is a block element.
            case "p" or "div" or "li" or "pre" when this._text.Length > 0
                                                    && this._text[^1] != _newLineCharacter:
                this._text.AppendLine();

                yield return this.CreateSnippet( contentInfo );

                this._text.Clear();

                break;

            case "pre":
                this._isTextPreformatted = false;

                break;
        }

        if ( this._nodeLevels.TryGetValue( node, out var finishedLevel ) )
        {
            this._headings.Reset( finishedLevel );
            this._anchor = this._anchors.TryGetValue( node, out var parentAnchor ) ? parentAnchor : null;
            this._nodeLevels.Remove( node );
            this._anchors.Remove( node );
        }

        if ( !skipNextSibling && node.NextSibling != null )
        {
            await foreach ( var snippet in this.CrawlRecursivelyAsync( node.NextSibling, parentNode, contentInfo ) )
            {
                yield return snippet;
            }
        }
    }

    private Snippet? CreateSnippet( ContentInfo contentInfo )
    {
        var trimmedText = this._text.ToString().Trim();

        if ( trimmedText == "" )
        {
            return null;
        }

        string title;

        switch ( contentInfo.Breadcrumb )
        {
            case "" when this._headings.AreEmpty:
                title = string.Join( " > ", contentInfo.Kinds.Concat( contentInfo.Categories ) );

                break;

            case "":
                title = this._headings.ToString();

                break;

            default:
                {
                    title = this._headings.AreEmpty
                        ? contentInfo.Breadcrumb
                        : string.Join( " > ", contentInfo.Breadcrumb, this._headings );

                    break;
                }
        }

        return new()
        {
            Title = title,
            Text = trimmedText,
            Link = this._anchor == null ? contentInfo.Url : $"{contentInfo.Url}#{this._anchor}",
            Source = contentInfo.Source,
            Products = contentInfo.Products,
            Kinds = contentInfo.Kinds,
            KindRank = contentInfo.KindRank,
            Categories = contentInfo.Categories,
            ComplexityLevels = contentInfo.ComplexityLevel < 100 ? Array.Empty<int>() : new[] { contentInfo.ComplexityLevel },
            ComplexityLevelRank = contentInfo.ComplexityLevelRank,
            NavigationLevel = this._headings.Level,
            NavigationLevelRank = this._headings.Rank
        };
    }

    private string GetNodeText( HtmlNode node ) => node.GetText( this._isTextPreformatted );

    private IEnumerable<Snippet?> CrawlTable( HtmlNode table, ContentInfo contentInfo )
    {
        // TODO: rowspan, colspan
        // TODO: Crawl children instead of using GetText.

        var headers = table.SelectNodes( ".//th" )?.Select( h => h.GetText() ).ToList() ?? new List<string>();

        foreach ( var row in table.SelectNodes( ".//tr" ) )
        {
            var rowText = string.Join(
                    Environment.NewLine,
                    row
                        ?.SelectNodes( "./td" )
                        ?.Select( ( c, i ) => $"{(i < headers.Count ? headers[i] : "_")}: {c.GetText()}" )
                    ?? Enumerable.Empty<string>() )
                .Trim();

            this._text.AppendLine( rowText );

            yield return this.CreateSnippet( contentInfo );

            this._text.Clear();
        }
    }
    
    // Make each first letter upper-case
    protected static string NormalizeCategoryName( string category )
    {
        if ( category.Length < 2 )
        {
            throw new InvalidOperationException( "Invalid category name." );
        }

        return string.Join( ' ', category.Split( ' ' ).Select( s => $"{char.ToUpperInvariant( s[0] )}{s.Substring( 1 )}" ) );
    }
}