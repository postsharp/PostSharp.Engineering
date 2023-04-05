// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Search.Backends;
using PostSharp.Engineering.BuildTools.Search.Indexers;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console.Cli;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace PostSharp.Engineering.BuildTools.Search;

public class UpdateSearchCommand : AsyncCommand<UpdateSearchCommandSettings>
{
    public override async Task<int> ExecuteAsync( CommandContext context, UpdateSearchCommandSettings settings )
    {
        if ( settings.Debug )
        {
            Debugger.Launch();
        }

        var console = new ConsoleHelper();
        SearchBackend search;

        if ( settings.Dry )
        {
            search = new ConsoleBackend( console );
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
            var collection = settings.Collection ?? "snippets";
            string source;
            string[] products;
            DocFxIndexer indexer;

            switch ( settings.Source )
            {
                case "metalamadoc":
                    indexer = new MetalamaDocIndexer( search, web, console );
                    source = "doc.metalama.net";
                    products = new[] { "Metalama" };

                    break;

                case "postsharpdoc":
                    indexer = new PostSharpDocIndexer( search, web, console );
                    source = "doc.postsharp.net";
                    products = new[] { "PostSharp" };
                    
                    break;
                
                default:
                    console.WriteError( $"Unknown source: '{settings.Source}'" );

                    return -1;
            }

            if ( settings.SinglePage )
            {
                await indexer.IndexArticlesAsync( collection, source, products, settings.SourceUrl );
            }
            else
            {
                await indexer.IndexSiteMapAsync( collection, source, products, settings.SourceUrl );
            }

            return 0;
        }
    }
}