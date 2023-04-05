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
using Typesense;

namespace PostSharp.Engineering.BuildTools.Search.Indexers;

public abstract class DocFxIndexer
{
    private readonly SearchBackend _search;
    private readonly HttpClient _client;
    private readonly ConsoleHelper _console;
    private readonly DocFxCrawler _crawler;

    protected DocFxIndexer( SearchBackend search, HttpClient client, ConsoleHelper console, DocFxCrawler crawler )
    {
        this._search = search;
        this._client = client;
        this._console = console;
        this._crawler = crawler;
    }

    public async Task IndexSiteMapAsync( bool dry, string collection, string source, string[] products, string url )
    {
        this._console.WriteMessage( $"Loading sitemap from '{url}'." );
        
        var documents = await new SiteMapCrawler( this._client ).GetDocumentsAsync( url );
        
        this._console.WriteMessage( "Sitemap loaded." );

        await this.IndexArticlesAsync( dry, collection, source, products, documents.ToArray() );
    }
    
    public async Task IndexArticlesAsync( bool dry, string collection, string source, string[] products, params string[] urls )
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
        List<ImportResponse> failedImports = new();
        List<Task> failedTasks = new();

        void StartBatch()
        {
            if ( dry )
            {
                batch.Clear();

                return;
            }
            
            var documentsInBatch = batch
                .Select(
                    s => s.Link!
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
            var completedTask = (Task<List<ImportResponse>>) await Task.WhenAny( tasks.ToArray() );

            try
            {
                var response = await completedTask;
                failedImports.AddRange( response.Where( r => !r.Success ) );
            }
            catch
            {
                failedTasks.Add( completedTask );
            }

            tasks.Remove( completedTask );
            finished++;

            Console.WriteLine(
                $"{sw.Elapsed}: Batch completed. Queued: {tasks.Count}; Finished: {finished}; Total: {t}; Parsed documents: {parsedDocuments.Count}/{urls.Length}" );
        }

        foreach ( var url in urls )
        {
            Stream stream;
            var httpTask = this._client.GetStreamAsync( url );

            try
            {
                stream = await httpTask;
            }
            catch
            {
                failedTasks.Add( httpTask );

                continue;
            }

            HtmlDocument document;
            
            await using ( stream )
            {
                document = new HtmlDocument();
                document.Load( stream );
            }

            await foreach ( var snippet in this._crawler.GetSnippetsFromDocument( document, url, source, products ) )
            {
                if ( tasks.Count == parallelism )
                {
                    await HandleNextCompletedTask( total );
                }

                if ( batch.Count == batchSize )
                {
                    StartBatch();
                }

                if ( dry )
                {
                    this._console.WriteMessage( snippet.Title! );
                    this._console.WriteMessage( "" );
                    this._console.WriteMessage( snippet.Text! );
                    this._console.WriteMessage( "" );
                    this._console.WriteMessage( $"Source: {snippet.Source}" );
                    this._console.WriteMessage( $"Link: {snippet.Link}" );
                    this._console.WriteMessage( $"Products: {string.Join( "; ", snippet.Products! )}" );
                    this._console.WriteMessage( $"Kinds: {string.Join( "; ", snippet.Kinds! )} ({snippet.KindRank})" );
                    this._console.WriteMessage( $"Categories: {string.Join( "; ", snippet.Categories! )}" );
                    this._console.WriteMessage( $"ComplexityLevels: {string.Join( "; ", snippet.ComplexityLevels! )} ({snippet.ComplexityLevelRank})" );
                    this._console.WriteMessage( $"NavigationLevel: {snippet.NavigationLevel} ({snippet.NavigationLevelRank})" );
                    this._console.WriteMessage( $"Rank: [{snippet.Rank}]" );
                    this._console.WriteMessage( "" );
                    this._console.WriteMessage( "--------------" );
                    this._console.WriteMessage( "" );
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

        if ( failedImports.Count > 0 || failedTasks.Count > 0 )
        {
            Console.WriteLine( $"{sw.Elapsed}: Indexing failed." );

            if ( failedTasks.Count > 0 )
            {
                Console.WriteLine( "Failed imports:" );

                failedImports.ForEach(
                    r =>
                    {
                        Console.WriteLine( r.Error );
                        Console.WriteLine( r.Document );
                        Console.WriteLine();
                    } );
            }

            if ( failedTasks.Count > 0 )
            {
                Console.WriteLine( "Exceptions:" );

                failedTasks.ForEach(
                    t =>
                    {
                        Console.WriteLine( t.Exception );
                    } );
            }
        }
        else
        {
            Console.WriteLine( $"{sw.Elapsed}: Indexing completed." );
        }
    }
}