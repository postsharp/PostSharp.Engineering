﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Search.Backends;
using PostSharp.Engineering.BuildTools.Search.Backends.Typesense;
using PostSharp.Engineering.BuildTools.Search.Crawlers;
using PostSharp.Engineering.BuildTools.Search.Indexers;
using PostSharp.Engineering.BuildTools.Utilities;
using System.Net.Http;
using System.Threading.Tasks;
using Typesense;

namespace PostSharp.Engineering.BuildTools.Search.Updaters;

public class DocumentationUpdater<TDocFxCrawler> : CollectionUpdater where TDocFxCrawler : DocFxCrawler, new()
{
    private readonly string[] _products;

    public DocumentationUpdater( string[] products, SearchBackend backend ) : base( backend )
    {
        this._products = products;
    }

    public override Task<bool> UpdateAsync( ConsoleHelper console, UpdateSearchCommandSettings settings, string targetCollection )
    {
        HttpClient web;

        if ( settings.IgnoreTls )
        {
            var handler = new HttpClientHandler();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;

            handler.ServerCertificateCustomValidationCallback =
                ( _, _, _, _ ) => true;

            web = new HttpClient( handler );
        }
        else
        {
            web = new HttpClient();
        }

        using ( web )
        {
            var indexer = new DocFxIndexer<TDocFxCrawler>( this.Backend, web, console );

            if ( settings.Single )
            {
                console.WriteMessage( $"Indexing single page '{settings.SourceUrl}' to '{targetCollection}' collection." );

                return indexer.IndexArticlesAsync( targetCollection, settings.Source, this._products, settings.SourceUrl );
            }
            else
            {
                console.WriteMessage( $"Indexing sitemap '{settings.SourceUrl}' to '{targetCollection}' collection." );

                return indexer.IndexSiteMapAsync( targetCollection, settings.Source, this._products, settings.SourceUrl );
            }
        }
    }

    public override Schema CreateSchema( string collectionName ) => CollectionSchemaFactory.CreateSchema<Snippet>( collectionName );
}