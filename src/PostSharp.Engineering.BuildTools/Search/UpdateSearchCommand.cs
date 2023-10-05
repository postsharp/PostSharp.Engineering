// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Search.Backends;
using PostSharp.Engineering.BuildTools.Search.Backends.Typesense;
using PostSharp.Engineering.BuildTools.Search.Updaters;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console.Cli;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PostSharp.Engineering.BuildTools.Search;

public abstract class UpdateSearchCommand : AsyncCommand<UpdateSearchCommandSettings>
{
    protected abstract CollectionUpdater CreateUpdater( SearchBackend backend );
    
    public override async Task<int> ExecuteAsync( CommandContext context, UpdateSearchCommandSettings settings )
    {
        var console = new ConsoleHelper();
        
        if ( settings.Debug )
        {
            console.WriteMessage( "Launching debugger." );
            Debugger.Launch();
        }

        CollectionUpdater updater;
        
        // When the collection is set explicitly, we don't work with an alias.
        var alias = settings.Collection == null ? settings.Source : null;
        string targetCollection;
        (string? Production, string Staging) targetCollections;

        if ( settings.Dry )
        {
            updater = this.CreateUpdater( new ConsoleBackend( console ) );
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
            var backend = new TypesenseBackend( apiKey, uri.Host, uri.Port.ToString( CultureInfo.InvariantCulture ), uri.Scheme );
            updater = this.CreateUpdater( backend );

            targetCollections = alias == null
                ? (null, settings.Collection!)
                : await GetTargetCollectionsForAliasAsync( backend, alias );

            targetCollection = targetCollections.Staging;
            
            console.WriteMessage( $"Resetting '{targetCollection}' collection." );
        }
        
        await ResetCollectionAsync( updater, targetCollection );

        var success = await updater.UpdateAsync( console, settings, targetCollection );

        if ( success && !settings.Dry && alias != null )
        {
            var sourceCollectionDescription = targetCollections.Production == null
                ? "none"
                : $"'{targetCollections.Production}' collection";
            
            console.WriteMessage( $"Swapping '{alias}' from {sourceCollectionDescription} to '{targetCollection}' collection." );
            await updater.Backend.UpsertCollectionAliasAsync( alias, targetCollection );
        }

        if ( success )
        {
            console.WriteMessage( "Done." );
        }
        else
        {
            console.WriteError( "Failed. See the error messages above." );
        }

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
    
    private static async Task ResetCollectionAsync( CollectionUpdater updater, string collection )
    {
        _ = await updater.Backend.TryDeleteCollectionAsync( collection );
        var schema = updater.CreateSchema( collection );
        await updater.Backend.CreateCollectionAsync( schema );
    }
}