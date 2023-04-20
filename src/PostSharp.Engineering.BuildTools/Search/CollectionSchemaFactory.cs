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
                new( "breadcrumb", FieldType.String ),
                new( "summary", FieldType.String ),
                new( "h1", FieldType.StringArray ),
                new( "h2", FieldType.StringArray ),
                new( "h3", FieldType.StringArray ),
                new( "h4", FieldType.StringArray ),
                new( "h5", FieldType.StringArray ),
                new( "h6", FieldType.StringArray ),
                new( "text", FieldType.StringArray ),
                new( "source", FieldType.String, true ),
                new( "link", FieldType.String ),
                new( "products", FieldType.StringArray, true ),
                new( "kinds", FieldType.StringArray, true ),
                new( "kind-rank", FieldType.Int32 ),
                new( "categories", FieldType.StringArray, true ),
                new( "complexity-levels", FieldType.Int32Array, true ),
                new( "complexity-level-rank", FieldType.Int32 ),
                new( "navigation-level", FieldType.Int32, true )
            } );
}