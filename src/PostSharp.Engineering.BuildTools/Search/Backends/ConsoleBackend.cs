// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PostSharp.Engineering.BuildTools.Search.Backends;

// TODO: Allow other types than just Snippet.
public class ConsoleBackend : SearchBackend
{
    private readonly ConsoleHelper _console;

    public ConsoleBackend( ConsoleHelper console )
    {
        this._console = console;
    }

    private void WriteSnippet( Snippet snippet )
    {
        this._console.WriteMessage( snippet.Title );
        this._console.WriteMessage( "" );
        this._console.WriteMessage( snippet.Text );
        this._console.WriteMessage( "" );
        this._console.WriteMessage( $"Source: {snippet.Source}" );
        this._console.WriteMessage( $"Link: {snippet.Link}" );
        this._console.WriteMessage( $"Products: {string.Join( "; ", snippet.Products )}" );
        this._console.WriteMessage( $"Kinds: {string.Join( "; ", snippet.Kinds )} ({snippet.KindRank})" );
        this._console.WriteMessage( $"Categories: {string.Join( "; ", snippet.Categories )}" );
        this._console.WriteMessage( $"ComplexityLevels: {string.Join( "; ", snippet.ComplexityLevels )} ({snippet.ComplexityLevelRank})" );
        this._console.WriteMessage( $"NavigationLevel: {snippet.NavigationLevel} ({snippet.NavigationLevelRank})" );
        this._console.WriteMessage( $"Rank: [{snippet.Rank}]" );
        this._console.WriteMessage( "" );
        this._console.WriteMessage( "--------------" );
        this._console.WriteMessage( "" );
    }

    private Task WriteSnippets( IEnumerable<Snippet> snippets )
    {
        foreach ( var snippet in snippets )
        {
            this.WriteSnippet( snippet );
        }

        return Task.CompletedTask;
    }

    public override Task CreateDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch ) => this.WriteSnippets( (IEnumerable<Snippet>) batch );

    public override Task UpsertDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch ) => this.WriteSnippets( (IEnumerable<Snippet>) batch );

    public override Task UpdateDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch ) => this.WriteSnippets( (IEnumerable<Snippet>) batch );

    public override Task EmplaceDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch ) => this.WriteSnippets( (IEnumerable<Snippet>) batch );
}