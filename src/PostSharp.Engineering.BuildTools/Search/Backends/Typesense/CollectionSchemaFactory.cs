// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;
using Typesense;

namespace PostSharp.Engineering.BuildTools.Search.Backends.Typesense;

public static class CollectionSchemaFactory
{
    public static Schema CreateSchema<T>( string collectionName )
    {
        var fields = new List<Field>();

        foreach ( var property in typeof(T).GetProperties() )
        {
            var jsonNameAttribute = property.GetCustomAttribute<JsonPropertyNameAttribute>()
                                    ?? throw new InvalidOperationException( $"Json property name not found for \"{nameof(T)}\".\"{property.Name}\" property." );

            FieldType type;

            if ( property.PropertyType.IsArray )
            {
                var arrayTypeCode = Type.GetTypeCode( property.PropertyType.GetElementType() );

                type = arrayTypeCode switch
                {
                    TypeCode.Int32 => FieldType.Int32Array,
                    TypeCode.String => FieldType.StringArray,
                    _ => throw new InvalidOperationException( $"Unknown array type code: \"{arrayTypeCode}\"" )
                };
            }
            else
            {
                var typeCode = Type.GetTypeCode( property.PropertyType );

                type = typeCode switch
                {
                    TypeCode.Int32 => FieldType.Int32,
                    TypeCode.String => FieldType.String,
                    _ => throw new InvalidOperationException( $"Unknown type code: \"{typeCode}\"" )
                };
            }

            var isFacet = property.GetCustomAttribute<FacetAttribute>() != null;

            fields.Add( new( jsonNameAttribute.Name, type, isFacet ) );
        }
        
        return new Schema( collectionName, fields );
    }

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