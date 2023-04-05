// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace PostSharp.Engineering.BuildTools.Search.Backends;

public abstract class SearchBackend
{
    public abstract Task CreateDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch );
    
    public abstract Task UpsertDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch );
    
    public abstract Task UpdateDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch );
    
    public abstract Task EmplaceDocumentsAsync<T>( string collection, IReadOnlyCollection<T> batch );
}