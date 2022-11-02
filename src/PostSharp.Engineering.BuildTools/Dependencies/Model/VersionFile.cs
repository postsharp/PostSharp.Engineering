// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using PostSharp.Engineering.BuildTools.Build;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Configuration.Provider;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

/// <summary>
/// Represents then information of <c>Versions.props</c>.
/// </summary>
public class VersionFile
{
    private static readonly Regex _dependencyVersionRegex = new(
        "^(?<Kind>[^:]+):(?<Arguments>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant );

    private static readonly Regex _buildSettingsRegex = new(
        @"^Number=(?<Number>\d+)(;TypeId=(?<TypeId>[^;]+))?(;TypeId=(?<Branch>[^;]+))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant );

    private VersionFile( ImmutableDictionary<string, DependencySource> dependencies )
    {
        this.Dependencies = dependencies;
    }

    public ImmutableDictionary<string, DependencySource> Dependencies { get; }

    public static bool TryRead( BuildContext context, [NotNullWhen( true )] out VersionFile? versionFile )
    {
        var dependenciesBuilder = ImmutableDictionary.CreateBuilder<string, DependencySource>();

        var versionsPath = Path.Combine( context.RepoDirectory, context.Product.VersionsFilePath );

        if ( !File.Exists( versionsPath ) )
        {
            context.Console.WriteError( $"The file '{versionsPath}' does not exist." );

            versionFile = null;

            return false;
        }

        var projectOptions = new ProjectOptions { GlobalProperties = new Dictionary<string, string>() { ["VcsBranch"] = context.Branch, ["DoNotLoadGeneratedVersionFiles"] = "True" } };
        var projectFile = Project.FromFile( versionsPath, projectOptions );

        var defaultDependencyProperties = context.Product.Dependencies
            .ToDictionary(
                d => d.Name,
                d => projectFile.Properties.SingleOrDefault( p => p.Name == d.NameWithoutDot + "Version" )
                    ?.EvaluatedValue );

        ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

        var versionsDocument = XDocument.Load( versionsPath, LoadOptions.PreserveWhitespace );
        
        foreach ( var dependencyDefinition in context.Product.Dependencies )
        {
            var dependencyVersion = defaultDependencyProperties[dependencyDefinition.Name];

            if ( dependencyVersion == null )
            {
                // This is possible and legal when the dependency does not have its own version.
                continue;
            }

            if ( !TryParseAndVerifyDependency( context, versionsPath, dependencyDefinition, dependencyVersion, versionsDocument, out var dependencySource ) )
            {
                versionFile = null;

                return false;
            }

            dependenciesBuilder[dependencyDefinition.Name] = dependencySource;
        }

        versionFile = new VersionFile( dependenciesBuilder.ToImmutable() );

        return true;
    }

    private static bool TryParseAndVerifyDependency(
        BuildContext context,
        string versionsPath,
        DependencyDefinition dependencyDefinition,
        string dependencyVersion,
        XDocument versionDocument,
        [NotNullWhen( true )] out DependencySource? dependencySource )
    {
        // Parse dependency version to create a dependency source from value provided in the dependency version.
        if ( dependencyVersion.Contains( "local", StringComparison.OrdinalIgnoreCase ) )
        {
            dependencySource = DependencySource.CreateLocal( DependencyConfigurationOrigin.Default );
        }
        else
        {
            var dependencyVersionMatch = _dependencyVersionRegex.Match( dependencyVersion );

            if ( dependencyVersionMatch.Success )
            {
                switch ( dependencyVersionMatch.Groups["Kind"].Value.ToLowerInvariant() )
                {
                    case "branch":
                        {
                            var branch = dependencyVersionMatch.Groups["Arguments"].Value;

                            dependencySource = DependencySource.CreateBuildServerSource(
                                new CiLatestBuildOfBranch( branch ),
                                DependencyConfigurationOrigin.Default );

                            break;
                        }

                    case "build":
                        {
                            var arguments = dependencyVersionMatch.Groups["Arguments"].Value;

                            var buildSettingsMatch = _buildSettingsRegex.Match( arguments );

                            if ( !buildSettingsMatch.Success )
                            {
                                context.Console.WriteError(
                                    $"The TeamCity build configuration '{arguments}' of dependency '{dependencyDefinition.Name}' does not have a correct format." );

                                dependencySource = null;

                                return false;
                            }

                            var buildNumber = int.Parse( buildSettingsMatch.Groups["Number"].Value, CultureInfo.InvariantCulture );
                            var ciBuildTypeId = buildSettingsMatch.Groups.GetValueOrDefault( "TypeId" )?.Value;

                            if ( string.IsNullOrEmpty( ciBuildTypeId ) )
                            {
                                context.Console.WriteError(
                                    $"The TypeId property of dependency '{dependencyDefinition.Name}' does is required in '{versionsPath}'." );
                            }

                            dependencySource = DependencySource.CreateBuildServerSource(
                                new CiBuildId( buildNumber, ciBuildTypeId ),
                                DependencyConfigurationOrigin.Default );

                            break;
                        }

                    case "transitive":
                        {
                            context.Console.WriteError( $"Error in '{versionsPath}': explicit transitive dependencies are no longer supported." );

                            dependencySource = null;

                            return false;
                        }

                    default:
                        {
                            context.Console.WriteError(
                                $"Error in '{versionsPath}': cannot parse the value '{dependencyVersion}' for dependency '{dependencyDefinition.Name}' in '{versionsPath}'." );

                            dependencySource = null;

                            return false;
                        }
                }
            }
            else if ( char.IsDigit( dependencyVersion[0] ) )
            {
                dependencySource = DependencySource.CreateFeed( dependencyVersion, DependencyConfigurationOrigin.Default );
            }
            else
            {
                context.Console.WriteError(
                    $"Error in '{versionsPath}': cannot parse the dependency '{dependencyDefinition.Name}' from '{versionsPath}'." );

                dependencySource = null;

                return false;
            }
        }

        // Verify dependency version property.
        var dependencyVersionPropertyName = $"{dependencyDefinition.NameWithoutDot}Version";
        var dependencyVersionProperty = versionDocument.Root!.Elements( "PropertyGroup" ).SingleOrDefault( p => p.Element( dependencyVersionPropertyName ) != null );

        if ( dependencyVersionProperty == null )
        {
            context.Console.WriteError( $"Error in '{versionsPath}': The property '{dependencyVersionPropertyName}' is missing." );

            return false;
        }

        // If the current product is on GitHub (i.e. the product requires dependency to have a public version),
        // we verify existence of version property with condition attribute for VcsBranch used in public version.
        if ( context.Product.DependencyDefinition.Repo.Provider == VcsProvider.GitHub && dependencyDefinition.RequiresPublicVersionOnGitHub )
        {
            // Verify dependency version property has a condition attribute.
            var publicVersionProperty = dependencyVersionProperty!.Elements( dependencyVersionPropertyName )
                .SingleOrDefault( e => e.HasAttributes && e.Attribute( "Condition" ) != null );

            if ( publicVersionProperty == null )
            {
                context.Console.WriteError(
                    $"Error in '{versionsPath}': The property '{dependencyVersionPropertyName}' with VcsBranch condition attribute is missing." );

                return false;
            }
        }

        return true;
    }
}