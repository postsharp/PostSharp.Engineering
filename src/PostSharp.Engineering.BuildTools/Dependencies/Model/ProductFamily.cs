// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public class ProductFamily
{
    private static int _areDependenciesInitialized;
    private static readonly Dictionary<string, Dictionary<string, ProductFamily>> _productFamilies = new();
    private readonly Dictionary<string, DependencyDefinition> _dependencyDefinitions = new();
    private readonly ProductFamily[] _relativeFamilies;

    public string Name { get; set; }

    public string Version { get; }

    public string VersionWithoutDots { get; }
    
    public ProductFamily? DownstreamProductFamily { get; init; }

    public ProductFamily( string name, string version, params ProductFamily[] relativeFamilies )
    {
        this.Name = name;
        this.Version = version;
        this.VersionWithoutDots = this.Version.Replace( ".", "", StringComparison.Ordinal );
        this._relativeFamilies = relativeFamilies;

        if ( !_productFamilies.TryGetValue( name, out var versions ) )
        {
            versions = new();
            _productFamilies.Add( name, versions );
        }

        versions.Add( version, this );
    }

    public static bool TryGetFamily( string name, string version, [NotNullWhen( true )] out ProductFamily? family )
    {
        if ( Interlocked.Exchange( ref _areDependenciesInitialized, 1 ) == 0 )
        {
            var dependencies = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany( a => a.GetTypes() )
                .Where(
                    t => t.GetProperties( BindingFlags.Public | BindingFlags.Static )
                        .Any( p => p.PropertyType.IsAssignableTo( typeof(DependencyDefinition) ) ) )
                .ToList();

            // Assert the namespace didn't change.
            if ( dependencies.Count == 0 )
            {
                throw new InvalidOperationException( "No default dependencies found." );
            }

            dependencies.ForEach( t => RuntimeHelpers.RunClassConstructor( t.TypeHandle ) );
        }

        if ( !_productFamilies.TryGetValue( name, out var versions ) )
        {
            family = null;

            return false;
        }

        return versions.TryGetValue( version, out family );
    }

    public bool TryGetDependencyDefinition( string name, [NotNullWhen( true )] out DependencyDefinition? definition )
    {
        if ( this._dependencyDefinitions.TryGetValue( name, out definition ) )
        {
            return true;
        }
        else
        {
            foreach ( var relatives in this._relativeFamilies )
            {
                if ( relatives.TryGetDependencyDefinition( name, out definition ) )
                {
                    return true;
                }
            }

            return false;
        }
    }

    public DependencyDefinition GetDependencyDefinition( string name )
        => this.TryGetDependencyDefinition( name, out var dependencyDefinition )
            ? dependencyDefinition
            : throw new KeyNotFoundException( $"'{name}' dependency definition not found in '{this.Name}' product family version '{this.Version}'." );

    public void Register( DependencyDefinition dependencyDefinition )
    {
        this._dependencyDefinitions.Add( dependencyDefinition.Name, dependencyDefinition );
    }

    public override string ToString() => $"{this.Name} {this.Version}";
}