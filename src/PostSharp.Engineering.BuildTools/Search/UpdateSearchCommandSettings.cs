// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Spectre.Console.Cli;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Search;

public class UpdateSearchCommandSettings : TypesenseCommandSettings
{
    [Description( "Name of the search source to retrieve data from. Valid values are: metalamadoc|postsharpdoc" )]
    [CommandArgument( 1, "<source>" )]
    public string Source { get; init; } = null!;

    [Description( "URL of the source." )]
    [CommandArgument( 2, "<source-url>" )]
    public string SourceUrl { get; init; } = null!;

    [Description( "Collection name to be updated. This parameter is used for development only." )]
    [CommandArgument( 3, "[collection]" )]
    public string? Collection { get; init; } = null;

    [Description(
        "Makes a single page to be crawled intead of all pages. Useful with --dry. When used, the <url> argument represents the signdle page to be crawled." )]
    [CommandOption( "--page" )]
    public bool SinglePage { get; init; } = false;

    [Description( "Does not change any data and writes the retrieved snippets to console. Use with --verbose." )]
    [CommandOption( "--dry" )]
    public bool Dry { get; init; } = false;
}