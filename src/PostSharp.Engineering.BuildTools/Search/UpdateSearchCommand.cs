// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Search.Backends;
using PostSharp.Engineering.BuildTools.Search.Indexers;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console.Cli;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PostSharp.Engineering.BuildTools.Search;

public class UpdateSearchCommand : AsyncCommand<UpdateSearchCommandSettings>
{
    public override async Task<int> ExecuteAsync( CommandContext context, UpdateSearchCommandSettings settings )
    {
        var console = new ConsoleHelper();
        
        if ( settings.Debug )
        {
            console.WriteMessage( "Launching debugger." );
            Debugger.Launch();
        }
        
        SearchBackend search;
        string source;
        string[] products;
        Func<SearchBackend, HttpClient, ConsoleHelper, DocFxIndexer> indexerFactory;

        switch ( settings.Source )
        {
            case "metalamadoc":
                indexerFactory = ( s, w, c ) => new MetalamaDocIndexer( s, w, c );
                source = "doc.metalama.net";
                products = new[] { "Metalama" };

                break;

            case "postsharpdoc":
                indexerFactory = ( s, w, c ) => new PostSharpDocIndexer( s, w, c );
                source = "doc.postsharp.net";
                products = new[] { "PostSharp" };
                    
                break;
                
            default:
                console.WriteError( $"Unknown source: '{settings.Source}'" );

                return -1;
        }
        
        // When the collection is set explicitly, we don't work with an alias.
        var alias = settings.Collection == null ? settings.Source : null;
        string targetCollection;
        (string? Production, string Staging) targetCollections;

        if ( settings.Dry )
        {
            search = new ConsoleBackend( console );
            targetCollection = "dry"; // Console backend doesn't work with collection names.
            targetCollections = (null, targetCollection);
        }
        else
        {
            const string apiKeyEnvironmentVariableName = "TYPESENSE_API_KEY";
            var apiKey = Environment.GetEnvironmentVariable( apiKeyEnvironmentVariableName );

            if ( apiKey == null )
            {
                console.WriteError( $"{apiKeyEnvironmentVariableName} environment variable not set." );

                return -1;
            }
            
            var uri = new Uri( settings.TypesenseUri );
            search = new TypesenseBackend( apiKey, uri.Host, uri.Port.ToString( CultureInfo.InvariantCulture ), uri.Scheme );

            targetCollections = alias == null
                ? (null, settings.Collection!)
                : await GetTargetCollectionsForAliasAsync( search, alias );

            targetCollection = targetCollections.Staging;
            
            console.WriteMessage( $"Resetting '{targetCollection}' collection." );
            await ResetCollectionAsync( search, targetCollection );
        }

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
            var indexer = indexerFactory( search, web, console );

            if ( settings.SinglePage )
            {
                console.WriteMessage( $"Indexing single page '{settings.SourceUrl}' to '{targetCollection}' collection." );
                await indexer.IndexArticlesAsync( targetCollection, source, products, settings.SourceUrl );
            }
            else
            {
                console.WriteMessage( $"Indexing sitemap '{settings.SourceUrl}' to '{targetCollection}' collection." );
                await indexer.IndexSiteMapAsync( targetCollection, source, products, settings.SourceUrl );
            }
        }

        if ( !settings.Dry && alias != null )
        {
            var sourceCollectionDescription = targetCollections.Production == null
                ? "none"
                : $"'{targetCollections.Production}' collection";
            
            console.WriteMessage( $"Swapping '{alias}' from {sourceCollectionDescription} to '{targetCollection}' collection." );
            await search.UpsertCollectionAliasAsync( alias, targetCollection );
        }
        
        console.WriteMessage( "Done." );
        
        return 0;
    }

    private static async Task<(string? Production, string Staging)> GetTargetCollectionsForAliasAsync( SearchBackend search, string alias )
    {
        var aliasResponses = await search.RetrieveCollectionAliasesAsync();
        var aliasResponse = aliasResponses.SingleOrDefault( a => a.Name == alias );

        if ( aliasResponse == null )
        {
            return (null, $"{alias}A");
        }

        var productionTarget = aliasResponse.CollectionName;
        var match = Regex.Match( productionTarget, $"{alias}(?<code>[AB])" );

        if ( !match.Success )
        {
            throw new InvalidOperationException( $"Unexpected target collection \"{productionTarget}\" of alias \"{alias}\"." );
        }

        var productionTargetCode = match.Groups["code"].Value;
        var stagingTargetCode = productionTargetCode == "A" ? "B" : "A";
        var stagingTarget = $"{alias}{stagingTargetCode}";

        return (productionTarget, stagingTarget);
    }
    
    private static async Task ResetCollectionAsync( SearchBackend search, string collection )
    {
        _ = await search.TryDeleteCollectionAsync( collection );
        var schema = CollectionSchemaFactory.CreateSnippetSchema( collection );
        await search.CreateCollectionAsync( schema );
    }
}