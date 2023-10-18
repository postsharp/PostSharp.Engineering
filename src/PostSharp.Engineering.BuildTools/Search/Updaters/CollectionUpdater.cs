// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Search.Backends;
using PostSharp.Engineering.BuildTools.Utilities;
using System.Threading.Tasks;
using Typesense;

namespace PostSharp.Engineering.BuildTools.Search.Updaters;

public abstract class CollectionUpdater
{
    public SearchBackendBase Backend { get; }

    protected CollectionUpdater( SearchBackendBase backend )
    {
        this.Backend = backend;
    }

    public abstract Task<bool> UpdateAsync( BuildContext context, UpdateSearchCommandSettings settings, string targetCollection );

    public abstract Schema CreateSchema( string collectionName );
}