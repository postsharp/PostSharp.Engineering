// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Generic;
using Typesense;

namespace PostSharp.Engineering.BuildTools.Search;

public static class CollectionSchemaFactory
{
    public static Schema CreateSnippetSchema( string collectionName )
        => new Schema(
            collectionName,
            new List<Field>
            {
                new( "title", FieldType.String ),
                new( "text", FieldType.String ),
                new( "source", FieldType.String, true ),
                new( "link", FieldType.String ),
                new( "products", FieldType.StringArray, true ),
                new( "kinds", FieldType.StringArray, true ),
                new( "categories", FieldType.StringArray, true ),
                new( "complexity-levels", FieldType.Int32Array, true ),
                new( "navigation-level", FieldType.Int32, true ),
                new( "rank", FieldType.Int32, false, false, true ),
            } );
}