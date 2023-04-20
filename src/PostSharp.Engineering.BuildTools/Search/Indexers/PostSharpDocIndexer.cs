// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Search.Backends;
using PostSharp.Engineering.BuildTools.Search.Crawlers;
using PostSharp.Engineering.BuildTools.Utilities;
using System.Net.Http;

namespace PostSharp.Engineering.BuildTools.Search.Indexers;

public class PostSharpDocIndexer : DocFxIndexer
{
    public PostSharpDocIndexer( SearchBackend search, HttpClient web, ConsoleHelper console )
        : base( search, web, console, () => new PostSharpDocCrawler() ) { }
}