﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Collections.Generic;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public class ProductFamily
{
    private readonly Dictionary<string, DependencyDefinition> _dependencyDefinitions = new();

    public string Version { get; }

    public string VersionWithoutDot { get; }

    public ProductFamily( string version )
    {
        this.Version = version;
        this.VersionWithoutDot = this.Version.Replace( ".", "", StringComparison.Ordinal );
    }

    public ProductFamily? DownstreamProductFamily { get; set; }

    public DependencyDefinition? GetDependencyDefinitionOrNull( string name )
    {
        this._dependencyDefinitions.TryGetValue( name, out var definition );

        return definition;
    }

    public DependencyDefinition GetDependencyDefinition( string name )
    {
        if ( !this._dependencyDefinitions.TryGetValue( name, out var definition ) )
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