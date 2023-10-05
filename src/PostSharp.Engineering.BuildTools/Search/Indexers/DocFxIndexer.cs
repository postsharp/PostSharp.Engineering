// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using HtmlAgilityPack;
using PostSharp.Engineering.BuildTools.Search.Backends;
using PostSharp.Engineering.BuildTools.Search.Crawlers;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PostSharp.Engineering.BuildTools.Search.Indexers;

public class DocFxIndexer<TDocFxCrawler> where TDocFxCrawler : DocFxCrawler, new()
{
    private readonly SearchBackendBase _search;
    private readonly HttpClient _web;
    private readonly ConsoleHelper _console;

    public DocFxIndexer( SearchBackendBase search, HttpClient web, ConsoleHelper console )
    {
        this._search = search;
        this._web = web;
        this._console = console;
    }

    public async Task<bool> IndexSiteMapAsync( string collection, string source, string[] products, string url )
    {
        this._console.WriteMessage( $"Loading sitemap from '{url}'." );
        
        var documents = await new SiteMapCrawler( this._web ).GetDocumentsAsync( url );
        
        this._console.WriteMessage( "Sitemap loaded." );

        return await this.IndexArticlesAsync( collection, source, products, documents.ToArray() );
    }
    
    public async Task<bool> IndexArticlesAsync( string collection, string source, string[] products, params string[] urls )
    {
        var sw = new Stopwatch();
        sw.Start();

        void WriteMessage( string message )
        {
            this._console.WriteMessage( $"{sw.Elapsed}: {message}" );
        }

        WriteMessage( "Indexing started." );
        
        const int parallelism = 8;
        const int batchSize = 40;
        var tasks = new List<Task>( parallelism );
        var batch = new List<Snippet>( batchSize );
        var finished = 0;
        var total = 0;
        var parsedDocuments = new HashSet<string>();
        List<Task> failedTasks = new();

        void StartBatch()
        {
            var documentsInBatch = batch
                .Select(
                    s => s.Link
                        .Contains( '#', StringComparison.Ordinal )
                        ? s.Link.Substring( 0, s.Link.IndexOf( '#', StringComparison.Ordinal ) )
                        : s.Link )
                .Distinct()
                .ToArray();

            parsedDocuments.UnionWith( documentsInBatch );
            var parsedDocumentsInBatch = string.Join( "; ", documentsInBatch );

            WriteMessage( $"Batch parsed. Starting indexing. (Partially) parsed documents: {parsedDocumentsInBatch}." );

            var task = this._search.CreateDocumentsAsync( collection, batch.ToImmutableArray() );
            tasks.Add( task );
            total++;
            batch.Clear();
        }

        async Task HandleNextCompletedTask( int t )
        {
            // TODO: Cancellation, delay
            var completedTask = await Task.WhenAny( tasks.ToArray() );

            try
            {
                await completedTask;
            }
            catch ( Exception e )
            {
                this._console.WriteError( e.ToString() );
                failedTasks.Add( completedTask );
            }

            tasks.Remove( completedTask );
            finished++;

            this._console.WriteMessage(
                $"{sw.Elapsed}: Batch completed. Queued: {tasks.Count}; Finished: {finished}; Total: {t}; Parsed documents: {parsedDocuments.Count}/{urls.Length}" );
        }

        foreach ( var url in urls )
        {
            Stream stream;
            var httpTask = this._web.GetStreamAsync( url );

            try
            {
                stream = await httpTask;
            }
            catch ( Exception e )
            {
                this._console.WriteError( e.ToString() );
                failedTasks.Add( httpTask );

                continue;
            }

            HtmlDocument document;
            
            await using ( stream )
            {
                document = new HtmlDocument();
                document.Load( stream );
            }

            var snippets = await new TDocFxCrawler().GetSnippetsFromDocument( document, source, products );
            
            foreach ( var snippet in snippets )
            {
                if ( tasks.Count == parallelism )
                {
                    await HandleNextCompletedTask( total );
                }

                if ( batch.Count == batchSize )
                {
                    StartBatch();
                }

                batch.Add( snippet );
            }
        }

        if ( batch.Count > 0 )
        {
            StartBatch();
        }

        while ( tasks.Count > 0 )
        {
            await HandleNextCompletedTask( total );
        }

        if ( failedTasks.Count > 0 )
        {
            this._console.WriteError( $"{sw.Elapsed}: Indexing failed." );

            this._console.WriteError( "Exceptions:" );

            failedTasks.ForEach(
                t =>
                {
                    this._console.WriteError( t.Exception?.ToString() ?? "<unknown>" );
                } );

            return false;
        }
        else
        {
            this._console.WriteMessage( $"{sw.Elapsed}: Indexing completed." );

            return true;
        }
    }
}