// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

/// <summary>
/// Represents then information of <c>Versions.props</c>.
/// </summary>
public class VersionFile
{
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

        var projectOptions = new ProjectOptions { GlobalProperties = new Dictionary<string, string>() { ["DoNotLoadGeneratedVersionFiles"] = "True" } };

        var projectFile = Project.FromFile( versionsPath, projectOptions );

        var defaultDependencyProperties = context.Product.Dependencies
            .ToDictionary(
                d => d.Name,
                d => projectFile.Properties.SingleOrDefault( p => p.Name == d.NameWithoutDot + "Version" )?.EvaluatedValue );

        ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

        foreach ( var dependencyDefinition in context.Product.Dependencies )
        {
            var dependencyVersion = defaultDependencyProperties[dependencyDefinition.Name];

            if ( dependencyVersion == null )
            {
                // A property is required because we update it during the release process.

                context.Console.WriteError( $"{versionsPath}: a property named '{dependencyDefinition.NameWithoutDot}Version' must exist, even with empty value." );

                continue;
            }

            dependencyVersion = dependencyVersion.Trim();

            // The property value can be either empty either a semantic version, but empty values are not allowed on guest devices,
            // i.e. for build outside of our VPN.

            if ( dependencyVersion != "" &&
                 !Regex.IsMatch(
                     dependencyVersion,
                     @"^[1-9]+.*$" ) )
            {
                context.Console.WriteError(
                    $"{versionsPath}: invalid value '{dependencyVersion}' for property '{dependencyDefinition.Name}Version': the value is neither empty nor a valid version number." );

                versionFile = null;

                return false;
            }

            // Set the default source of the dependency according to the build context.
            DependencySource dependencySource;

            if ( BuildContext.IsGuestDevice || !dependencyDefinition.GenerateSnapshotDependency )
            {
                if ( dependencyVersion == "" )
                {
                    context.Console.WriteError( $"{versionsPath}: missing value for property '{dependencyDefinition.NameWithoutDot}Version'." );

                    versionFile = null;

                    return false;
                }

                dependencySource = DependencySource.CreateFeed( dependencyVersion, DependencyConfigurationOrigin.Default );
            }
            else if ( TeamCityHelper.IsTeamCityBuild() )
            {
                dependencySource = DependencySource.CreateRestoredDependency( context, dependencyDefinition, DependencyConfigurationOrigin.Default );
            }
            else
            {
                dependencySource = DependencySource.CreateBuildServerSource(
                    new CiLatestBuildOfBranch( dependencyDefinition.DefaultBranch ),
                    DependencyConfigurationOrigin.Default );
            }

            dependenciesBuilder[dependencyDefinition.Name] = dependencySource;
        }

        versionFile = new VersionFile( dependenciesBuilder.ToImmutable() );

        return true;
    }

    public static bool Validate( BuildContext context, DependenciesOverrideFile dependenciesOverrideFile )
    {
        var versionsPath = Path.Combine( context.RepoDirectory, context.Product.VersionsFilePath );
        var document = XDocument.Load( versionsPath );
        var hasError = false;

        foreach ( var dependency in dependenciesOverrideFile.Dependencies.Keys )
        {
            var dependencyDefinition = Model.Dependencies.All.SingleOrDefault( d => d.Name == dependency )
                                       ?? TestDependencies.All.Single( d => d.Name == dependency );

            var propertyName = $"{dependencyDefinition.NameWithoutDot}Version";

            var elements = document.Root!.XPathSelectElements( $"/Project/PropertyGroup/{propertyName}" ).ToList();

            switch ( elements.Count )
            {
                case > 1:
                    context.Console.WriteError( $"{versionsPath}: the file contains more than one definition of the '{propertyName}' property." );
                    hasError = true;

                    break;

                case 1 when elements[0].HasAttributes:
                    context.Console.WriteError( $"{versionsPath}: the '{propertyName}' property definition should not have any attribute." );
                    hasError = true;

                    break;
            }
        }

        return !hasError;
    }
}