// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
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

    public override Task CreateDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch )
        => this._client.ImportDocuments<T>( collection, batch, batch.Count, ImportType.Create );

    public override Task UpsertDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch )
        => this._client.ImportDocuments<T>( collection, batch, batch.Count, ImportType.Upsert );

    public override Task UpdateDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch )
        => this._client.ImportDocuments<T>( collection, batch, batch.Count, ImportType.Update );

    public override Task EmplaceDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch )
        => this._client.ImportDocuments<T>( collection, batch, batch.Count, ImportType.Emplace );
}