// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
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

    /// <summary>
    /// Guards every read and write of the registries below. Families and their dependency definitions are registered
    /// from the static constructors of the definition classes, and the runtime runs the constructors of different
    /// classes on different threads, so those registries are written concurrently even though each class is
    /// initialized once. Without this, a test run that touches several definition classes at once corrupts the
    /// dictionaries, and the failure surfaces far from its cause as a type initialization error.
    /// </summary>
    private static readonly object _sync = new();

    private static readonly Dictionary<string, Dictionary<string, ProductFamily>> _productFamilies = new();
    private readonly Dictionary<string, DependencyDefinition> _dependencyDefinitions = new();
    private readonly Dictionary<string, DependencyDefinition> _dependencyDefinitionsByCiId = new();
    private readonly ProductFamily[] _relativeFamilies;

    public string? ConsolidatedProjectName { get; init; }

    public string Name { get; set; }

    public string Version { get; }

    public string VersionWithoutDots { get; }

    public ProductFamily? UpstreamProductFamily { get; init; }

    /// <summary>
    /// Gets the identifier of the TeamCity GitHub App connection that issues the build-scoped token for the
    /// repositories of this family. See <see cref="GitHubAppConnections"/>. A repository of this family that belongs to
    /// another GitHub organization must override this value with <see cref="DependencyDefinition.GitHubAppConnectionId"/>.
    /// </summary>
    public string? GitHubAppConnectionId { get; init; }

    public BuildAgentRequirements DefaultBuildAgentRequirements { get; init; } = BuildAgentRequirements.Default;

    /// <summary>
    /// Gets the preferred versions of the .NET SDK and of the .NET runtime for the repositories of this family. A
    /// repository is free to use another version, but sharing the versions inside a family increases the reuse of
    /// Docker layers between the build images of the repositories of the family.
    /// </summary>
    public PreferredDotNetVersions PreferredVersions { get; init; } = PreferredDotNetVersions.Default;

    public bool HasConsolidatedProduct => this.ConsolidatedProjectName != null;

    public ProductFamily( string name, string version, params ProductFamily[] relativeFamilies )
    {
        this.Name = name;
        this.Version = version;
        this.VersionWithoutDots = this.Version.Replace( ".", "", StringComparison.Ordinal );
        this._relativeFamilies = relativeFamilies;

        lock ( _sync )
        {
            if ( !_productFamilies.TryGetValue( name, out var versions ) )
            {
                versions = new Dictionary<string, ProductFamily>();
                _productFamilies.Add( name, versions );
            }

            versions.Add( version, this );
        }
    }

    public static bool TryGetFamily( string name, string version, [NotNullWhen( true )] out ProductFamily? family )
    {
        if ( Interlocked.Exchange( ref _areDependenciesInitialized, 1 ) == 0 )
        {
            var dependencies = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany( a => a.GetTypes() )
                .Where( t => t.GetProperties( BindingFlags.Public | BindingFlags.Static )
                            .Any( p => p.PropertyType.IsAssignableTo( typeof(DependencyDefinition) ) ) )
                .ToList();

            // Assert the namespace didn't change.
            if ( dependencies.Count == 0 )
            {
                throw new InvalidOperationException( "No default dependencies found." );
            }

            dependencies.ForEach( t => RuntimeHelpers.RunClassConstructor( t.TypeHandle ) );
        }

        // Deliberately not holding the lock while the class constructors run above: one of them registers a family and
        // would wait for this lock, while this thread waits for the runtime to finish initializing that same class.
        lock ( _sync )
        {
            if ( !_productFamilies.TryGetValue( name, out var versions ) )
            {
                family = null;

                return false;
            }

            return versions.TryGetValue( version, out family );
        }
    }

    public bool TryGetDependencyDefinition( string name, [NotNullWhen( true )] out DependencyDefinition? definition )
        => this.TryGetDependencyDefinition( name, f => f._dependencyDefinitions, out definition );

    public bool TryGetDependencyDefinitionByCiId( string name, [NotNullWhen( true )] out DependencyDefinition? definition )
        => this.TryGetDependencyDefinition( name, f => f._dependencyDefinitionsByCiId, out definition );

    private bool TryGetDependencyDefinition(
        string name,
        Func<ProductFamily, IReadOnlyDictionary<string, DependencyDefinition>> getDependencyDefinitions,
        [NotNullWhen( true )] out DependencyDefinition? definition )
    {
        lock ( _sync )
        {
            if ( getDependencyDefinitions( this ).TryGetValue( name, out definition ) )
            {
                return true;
            }
        }

        // Outside the lock: the relatives are searched through this same method, which takes it again.
        foreach ( var relatives in this._relativeFamilies )
        {
            if ( relatives.TryGetDependencyDefinition( name, getDependencyDefinitions, out definition ) )
            {
                return true;
            }
        }

        definition = null;

        return false;
    }

    public DependencyDefinition GetDependencyDefinition( string name )
        => this.TryGetDependencyDefinition( name, out var dependencyDefinition )
            ? dependencyDefinition
            : throw new KeyNotFoundException( $"'{name}' dependency definition not found in '{this.Name}' product family version '{this.Version}'." );

    public void Register( DependencyDefinition dependencyDefinition )
    {
        lock ( _sync )
        {
            this._dependencyDefinitions.Add( dependencyDefinition.Name, dependencyDefinition );

            // Two definitions of the same repository can share a continuous-integration project: one describes how the
            // repository is built, the other how the packages it publishes are consumed. PostSharp 2026.0 is the case in
            // point -- 'PostSharp' builds from the development branch, while 'PostSharpPackage' resolves the signed
            // distribution from the release branch -- and both belong to PostSharpGitHub_PostSharp20260. This index
            // answers "which product is built here", so the versioned definition is the one it must return; the
            // consuming definition takes the entry only when no versioned definition has claimed the project.
            var ciProjectId = dependencyDefinition.CiConfiguration.ProjectId.Id;

            if ( !this._dependencyDefinitionsByCiId.TryGetValue( ciProjectId, out var registeredDefinition ) )
            {
                this._dependencyDefinitionsByCiId.Add( ciProjectId, dependencyDefinition );
            }
            else if ( dependencyDefinition.IsVersioned && registeredDefinition.IsVersioned )
            {
                throw new InvalidOperationException(
                    $"'{dependencyDefinition.Name}' and '{registeredDefinition.Name}' are both versioned definitions of the "
                    + $"continuous-integration project '{ciProjectId}' in '{this}'. A project builds a single product." );
            }
            else if ( dependencyDefinition.IsVersioned )
            {
                this._dependencyDefinitionsByCiId[ciProjectId] = dependencyDefinition;
            }
        }
    }

    public override string ToString() => $"{this.Name} {this.Version}";
}