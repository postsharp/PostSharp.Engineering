// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Typesense;

namespace PostSharp.Engineering.BuildTools.Search.Backends;

public class ConsoleBackend : SearchBackend
{
    private readonly ConsoleHelper _console;

    public ConsoleBackend( ConsoleHelper console )
    {
        this._console = console;
    }

    public override Task CreateCollectionAsync( Schema schema )
    {
        this._console.WriteMessage( $"Collection: {schema.Name}" );

        foreach ( var field in schema.Fields )
        {
            this._console.WriteMessage( $"  {field.Name}: {field.Type}, facet: {field.Facet}" );
        }
        
        this._console.WriteMessage( "" );

        return Task.CompletedTask;
    }

    public override Task DeleteCollectionAsync( string collection ) => throw new NotSupportedException();
    
    public override Task<bool> TryDeleteCollectionAsync( string collection ) => Task.FromResult( false );

    public override Task<IEnumerable<CollectionResponse>> RetrieveCollectionsAsync() => throw new NotSupportedException();

    public override Task UpsertCollectionAliasAsync( string alias, string targetCollection ) => throw new NotSupportedException();

    public override Task DeleteCollectionAliasAsync( string alias ) => throw new NotSupportedException();

    public override Task<IEnumerable<CollectionAliasResponse>> RetrieveCollectionAliasesAsync() => throw new NotSupportedException();

    public override Task<string> GetTargetOfCollectionAliasAsync( string alias ) => throw new NotSupportedException();

    private void WriteDocument<T>( T document )
    {
        foreach ( var property in document!.GetType().GetProperties() )
        {
            var value = property.GetValue( document );
            
            if ( property.GetType().IsArray )
            {
                this._console.WriteMessage( property.Name );

                foreach ( var item in (IEnumerable) value! )
                {
                    this._console.WriteMessage( $"  {item}" );
                }
            }
            else
            {
                this._console.WriteMessage( $"{property.Name}: {value}" );
            }
        }
        
        this._console.WriteMessage( "" );
    }

    private Task WriteDocuments<T>( IEnumerable<T> documents )
    {
        foreach ( var document in documents )
        {
            this.WriteDocument( document );
        }

        return Task.CompletedTask;
    }

    public override Task CreateDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch ) => this.WriteDocuments( batch );

    public override Task UpsertDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch ) => this.WriteDocuments( batch );

    public override Task UpdateDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch ) => this.WriteDocuments( batch );

    public override Task EmplaceDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch ) => this.WriteDocuments( batch );
}