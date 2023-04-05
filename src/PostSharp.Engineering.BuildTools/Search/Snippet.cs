// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace PostSharp.Engineering.BuildTools.Search;

public class Snippet
{
    [JsonPropertyName( "title" )]
    public string Title { get; set; } = null!;

    [JsonPropertyName( "text" )]
    public string Text { get; set; } = null!;

    [JsonPropertyName( "source" )]
    public string Source { get; set; } = null!;

    [JsonPropertyName( "link" )]
    public string Link { get; set; } = null!;

    [JsonPropertyName( "products" )]
    public string[] Products { get; set; } = null!;

    [JsonPropertyName( "kinds" )]
    public string[] Kinds { get; set; } = null!;

    [JsonIgnore]
    [Range( 0, 99 )]
    public int KindRank { get; set; }

    [JsonPropertyName( "categories" )]
    public string[] Categories { get; set; } = null!;

    [JsonPropertyName( "complexity-levels" )]
    public int[] ComplexityLevels { get; set; } = null!;

    [JsonIgnore]
    [Range( 0, 999 )]
    public int ComplexityLevelRank { get; set; }

    [JsonPropertyName( "navigation-level" )]
    public int NavigationLevel { get; set; }

    [JsonIgnore]
    [Range( 0, 99 )]
    public int NavigationLevelRank { get; set; }

    [JsonPropertyName( "rank" )]
    public int Rank
    {
        get
        {
            this.GetType()
                .GetProperties()
                .Select<PropertyInfo, (PropertyInfo, RangeAttribute)?>(
                    p =>
                    {
                        var range = p.GetCustomAttributes<RangeAttribute>().SingleOrDefault();

                        if ( range == null )
                        {
                            return null;
                        }
                        else
                        {
                            return (p, range);
                        }
                    } )
                .ToList()
                .ForEach(
                    x =>
                    {
                        if ( !x.HasValue )
                        {
                            return;
                        }

                        var value = (int) x.Value.Item1.GetValue( this )!;
                        var range = x.Value.Item2;

                        if ( value < (int) range.Minimum || value > (int) range.Maximum )
                        {
                            throw new ArgumentOutOfRangeException(
                                nameof(value),
                                $"The value of '{x.Value.Item1.Name}' property is required to be in range {range.Minimum}-{range.Maximum}." );
                        }
                    } );

            return (this.KindRank * 1_000_00) + (this.ComplexityLevelRank * 1_00) + (this.NavigationLevelRank * 1);
        }
    }
}