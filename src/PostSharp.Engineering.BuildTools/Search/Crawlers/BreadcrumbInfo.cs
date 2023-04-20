using HtmlAgilityPack;
using System;

namespace PostSharp.Engineering.BuildTools.Search.Crawlers;

public record BreadcrumbInfo(
    string Breadcrumb,
    string[] Kinds,
    int KindRank,
    string[] Categories,
    int NavigationLevel,
    bool IsPageIgnored,
    bool IsApiDoc );