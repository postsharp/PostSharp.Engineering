// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PostSharp.Engineering.BuildTools.Search.Crawlers;

public abstract class DocFxCrawler
{
    private static readonly char _newLineCharacter = Environment.NewLine[^1];
    private ContentInfo? _contentInfo;
    private readonly List<string> _h1 = new();
    private readonly List<string> _h2 = new();
    private readonly List<string> _h3 = new();
    private readonly List<string> _h4 = new();
    private readonly List<string> _h5 = new();
    private readonly List<string> _h6 = new();
    private readonly IReadOnlyDictionary<int, List<string>> _headings;
    private readonly StringBuilder _textBuilder = new StringBuilder();
    private readonly List<string> _text = new();
    private bool _isTextPreformatted;
    private bool _isParagraphIgnored;

    protected DocFxCrawler()
    {
        this._headings = new Dictionary<int, List<string>>
        {
            { 1, this._h1 },
            { 2, this._h2 },
            { 3, this._h3 },
            { 4, this._h4 },
            { 5, this._h5 },
            { 6, this._h6 }
        }.ToImmutableDictionary();
    }

    public async IAsyncEnumerable<Snippet> GetSnippetsFromDocument(
        HtmlDocument document,
        string url,
        string source,
        string[] products )
    {
        Console.WriteLine( $"--- {url}" );

        // TODO: The design could be improved, so we don't need to check this.
        if ( this._contentInfo != null )
        {
            throw new InvalidOperationException( "This object is not reusable." );
        }
        
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

        var complexityLevelRank = 1000 - complexityLevel - 1;

        this._contentInfo = new(
            breadcrumb.Breadcrumb,
            url,
            breadcrumb.Kinds,
            breadcrumb.KindRank,
            breadcrumb.Categories,
            source,
            products,
            complexityLevel,
            complexityLevelRank,
            breadcrumb.NavigationLevel,
            breadcrumb.IsNextParagraphIgnored );

        await foreach ( var snippet in this.CrawlAsync(
                           document.DocumentNode
                               .SelectSingleNode( "//div[@id=\"content\"]" ) ) )
        {
            if ( snippet != null )
            {
                yield return snippet;
            }
        }
    }

    protected abstract BreadcrumbInfo GetBreadcrumbData( HtmlNode[] breadcrumbLinks );

    private async IAsyncEnumerable<Snippet?> CrawlAsync( HtmlNode node )
    {
        await this.CrawlRecursivelyAsync( node, true );

        this.FinishParagraph();

        yield return this.CreateSnippet();
    }

    private async Task CrawlRecursivelyAsync( HtmlNode node, bool skipNextSibling = false )
    {
        await Task.Yield();

        var skipChildNodes = false;
        var nodeName = node.Name.ToLowerInvariant();

        if ( Regex.IsMatch( nodeName, @"^h\d$" ) )
        {
            this.FinishParagraph();

            var level = node.Name[1] - '0';
            var text = this.GetNodeText( node );
            this._headings[level].Add( text );
            this._isParagraphIgnored = this._contentInfo!.IsNextParagraphIgnored( node );

            skipChildNodes = true;
        }
        else if ( node.NodeType == HtmlNodeType.Text )
        {
            var innerText = this.GetNodeText( node );

            if ( innerText != "" )
            {
                if ( this._textBuilder.Length > 0 && char.IsLetterOrDigit( innerText[0] ) && !char.IsWhiteSpace( this._textBuilder[^1] ) )
                {
                    this._textBuilder.Append( ' ' );
                }

                this._textBuilder.Append( innerText );
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
                    this._textBuilder.Append( "- " );

                    break;

                case "pre":
                    this._isTextPreformatted = true;

                    break;

                case "table":
                    this.CrawlTable( node );

                    skipChildNodes = true;

                    break;
            }
        }

        if ( !skipChildNodes && node.HasChildNodes )
        {
            await this.CrawlRecursivelyAsync( node.FirstChild );
        }

        switch ( nodeName )
        {
            case "br":
                this._textBuilder.AppendLine();

                break;

            // TODO: We could improve this by figuring out if the node is a block element.
            case "p" or "div" or "li" or "pre" when this._textBuilder.Length > 0
                                                    && this._textBuilder[^1] != _newLineCharacter:
                this._textBuilder.AppendLine();
                this.FinishParagraph();

                break;

            case "pre":
                this._isTextPreformatted = false;

                break;
        }

        if ( !skipNextSibling && node.NextSibling != null )
        {
            await this.CrawlRecursivelyAsync( node.NextSibling );
        }
    }

    private void FinishParagraph()
    {
        // TODO: The performance can be improved by skipping the ignored nodes,
        // but it's not worth the effort at the moment.
        if ( !this._isParagraphIgnored )
        {
            var text = this._textBuilder.ToString().Trim();

            if ( text.Length > 0 )
            {
                this._text.Add( text );
            }
        }

        this._textBuilder.Clear();
    }
    
    private Snippet? CreateSnippet()
    {
        if ( this._contentInfo == null )
        {
            throw new InvalidOperationException( $"{nameof(this._contentInfo)} field is not set." );
        }

        return new()
        {
            Breadcrumb = this._contentInfo.Breadcrumb,
            Summary = "", // TODO
            Text = this._text.ToArray(),
            H1 = this._h1.ToArray(),
            H2 = this._h2.ToArray(),
            H3 = this._h3.ToArray(),
            H4 = this._h4.ToArray(),
            H5 = this._h5.ToArray(),
            H6 = this._h6.ToArray(),
            Link = this._contentInfo.Url,
            Source = this._contentInfo.Source,
            Products = this._contentInfo.Products,
            Kinds = this._contentInfo.Kinds,
            KindRank = this._contentInfo.KindRank,
            Categories = this._contentInfo.Categories,
            ComplexityLevels = this._contentInfo.ComplexityLevel < 100 ? Array.Empty<int>() : new[] { this._contentInfo.ComplexityLevel },
            ComplexityLevelRank = this._contentInfo.ComplexityLevelRank,
            NavigationLevel = this._contentInfo.NavigationLevel
        };
    }

    private string GetNodeText( HtmlNode node ) => node.GetText( this._isTextPreformatted );

    private void CrawlTable( HtmlNode table )
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

            this._textBuilder.AppendLine( rowText );
            this.FinishParagraph();
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