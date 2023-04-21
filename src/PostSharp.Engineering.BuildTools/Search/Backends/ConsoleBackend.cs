// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Typesense;

namespace PostSharp.Engineering.BuildTools.Search.Backends;

// TODO: Allow other types than just Snippet.
public class ConsoleBackend : SearchBackend
{
    private readonly ConsoleHelper _console;

    public ConsoleBackend( ConsoleHelper console )
    {
        this._console = console;
    }

    public override Task CreateCollectionAsync( Schema schema ) => throw new NotSupportedException();

    public override Task DeleteCollectionAsync( string collection ) => throw new NotSupportedException();
    
    public override Task<bool> TryDeleteCollectionAsync( string collection ) => throw new NotSupportedException();

    public override Task<IEnumerable<CollectionResponse>> RetrieveCollectionsAsync() => throw new NotSupportedException();

    public override Task UpsertCollectionAliasAsync( string alias, string targetCollection ) => throw new NotSupportedException();

    public override Task DeleteCollectionAliasAsync( string alias ) => throw new NotSupportedException();

    public override Task<IEnumerable<CollectionAliasResponse>> RetrieveCollectionAliasesAsync() => throw new NotSupportedException();

    public override Task<string> GetTargetOfCollectionAliasAsync( string alias ) => throw new NotSupportedException();

    private void WriteSnippet( Snippet snippet )
    {
        this._console.WriteMessage( $"Title: {snippet.Title}" );
        this._console.WriteMessage( $"Breadcrumb: {snippet.Breadcrumb}" );
        this._console.WriteMessage( $"H1: {string.Join( "; ", snippet.H1 )}" );
        this._console.WriteMessage( $"H2: {string.Join( "; ", snippet.H2 )}" );
        this._console.WriteMessage( $"H3: {string.Join( "; ", snippet.H3 )}" );
        this._console.WriteMessage( $"H4: {string.Join( "; ", snippet.H4 )}" );
        this._console.WriteMessage( $"H5: {string.Join( "; ", snippet.H5 )}" );
        this._console.WriteMessage( $"H6: {string.Join( "; ", snippet.H6 )}" );
        this._console.WriteMessage( "" );
        this._console.WriteMessage( "Summary:" );
        this._console.WriteMessage( snippet.Summary );
        this._console.WriteMessage( "" );
        this._console.WriteMessage( "Text:" );
        this._console.WriteMessage( string.Join( $"{Environment.NewLine}{Environment.NewLine}", snippet.Text ) );
        this._console.WriteMessage( "" );
        this._console.WriteMessage( $"Source: {snippet.Source}" );
        this._console.WriteMessage( $"Link: {snippet.Link}" );
        this._console.WriteMessage( $"Products: {string.Join( "; ", snippet.Products )}" );
        this._console.WriteMessage( $"Kinds: {string.Join( "; ", snippet.Kinds )} ({snippet.KindRank})" );
        this._console.WriteMessage( $"Categories: {string.Join( "; ", snippet.Categories )}" );
        this._console.WriteMessage( $"ComplexityLevels: {string.Join( "; ", snippet.ComplexityLevels )} ({snippet.ComplexityLevelRank})" );
        this._console.WriteMessage( $"NavigationLevel: {snippet.NavigationLevel}" );
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