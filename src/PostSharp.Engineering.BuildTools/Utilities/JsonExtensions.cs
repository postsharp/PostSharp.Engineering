// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Text.Json;

namespace PostSharp.Engineering.BuildTools.Utilities;

internal static class JsonExtensions
{
    public static JsonElement? GetPropertyOrNull( this JsonElement? element, string propertyName )
    {
        if ( element.HasValue && element.Value.TryGetProperty( propertyName, out var value ) )
        {
            return value;
        }
        else
        {
            return null;
        }
    }

    public static JsonElement? GetPropertyOrNull( this JsonElement element, string propertyName )
    {
        if ( element.TryGetProperty( propertyName, out var value ) )
        {
            return value;
        }
        else
        {
            return null;
        }
    }
}