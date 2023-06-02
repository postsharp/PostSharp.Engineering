// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Collections.Generic;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public class ProductFamily
{
    private readonly Dictionary<string, DependencyDefinition> _dependencyDefinitions = new();

    private readonly ProductFamily[] _relativeFamilies;

    public string Version { get; }

    public string VersionWithoutDots { get; }

    public ProductFamily( string version, params ProductFamily[] relativeFamilies )
    {
        this.Version = version;
        this.VersionWithoutDots = this.Version.Replace( ".", "", StringComparison.Ordinal );
        this._relativeFamilies = relativeFamilies;
    }

    public ProductFamily? DownstreamProductFamily { get; set; }

    public DependencyDefinition? GetDependencyDefinitionOrNull( string name )
    {
        if ( !this._dependencyDefinitions.TryGetValue( name, out var definition ) )
        {
            foreach ( var relatives in this._relativeFamilies )
            {
                definition = relatives.GetDependencyDefinitionOrNull( name );

                if ( definition != null )
                {
                    return definition;
                }
            }
        }

        return definition;
    }

    public DependencyDefinition GetDependencyDefinition( string name )
    {
        var definition = this.GetDependencyDefinitionOrNull( name );

        if ( definition == null )
        {
            throw new KeyNotFoundException( $"The dependency '{name}' does not exist." );
        }

        return definition;
    }

    public void Register( DependencyDefinition dependencyDefinition )
    {
        this._dependencyDefinitions.Add( dependencyDefinition.Name, dependencyDefinition );
    }
}