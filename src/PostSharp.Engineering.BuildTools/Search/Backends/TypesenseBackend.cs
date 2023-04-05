// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Typesense;
using Typesense.Setup;

namespace PostSharp.Engineering.BuildTools.Search.Backends;

public class TypesenseBackend : SearchBackend
{
    private readonly ITypesenseClient _client;

    public TypesenseBackend( string apiKey, string host, string port, string protocol = "http" )
    {
        var services = new ServiceCollection()
            .AddTypesenseClient(
                config =>
                {
                    config.ApiKey = apiKey;
                    config.Nodes = new List<Node> { new Node( host, port, protocol ) };
                } )
            .BuildServiceProvider();

        this._client = services.GetService<ITypesenseClient>()!;
    }

    private async Task CreateDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch, ImportType importType )
    {
        var responses = await this._client.ImportDocuments<T>( collection, batch, batch.Count, importType );
        var failedImports = responses.Where( r => !r.Success ).ToArray();

        if ( failedImports.Length != 0 )
        {
            throw new TypesenseImportFailedException( failedImports );
        }
    }

    public override Task CreateDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch )
        => this.CreateDocumentsAsync( collection, batch, ImportType.Create );

    public override Task UpsertDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch )
        => this.CreateDocumentsAsync( collection, batch, ImportType.Upsert );

    public override Task UpdateDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch )
        => this.CreateDocumentsAsync( collection, batch, ImportType.Update );

    public override Task EmplaceDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch )
        => this.CreateDocumentsAsync( collection, batch, ImportType.Emplace );
}