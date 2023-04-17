using HtmlAgilityPack;
using System;

namespace PostSharp.Engineering.BuildTools.Search;

public record BreadcrumbInfo(
    string Breadcrumb,
    string[] Kinds,
    int KindRank,
    string[] Categories,
    int NavigationLevel,
    Func<HtmlNode, bool> IsNextParagraphIgnored );