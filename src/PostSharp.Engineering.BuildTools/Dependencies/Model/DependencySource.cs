// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public sealed class DependencySource
    {
        /// <summary>
        /// Gets the NuGet version of the dependency, if <see cref="SourceKind"/> is set to <see cref="DependencySourceKind.Feed"/>.
        /// </summary>
        public string? Version { get; private init; }

        public ICiBuildSpec? BuildServerSource { get; internal set; }

        internal string? VersionFile { get; set; }

        public DependencySourceKind SourceKind { get; internal set; }

        public DependencyConfigurationOrigin Origin { get; internal init; }

        public static DependencySource CreateLocalRepo( DependencyConfigurationOrigin origin )
            => new() { Origin = origin, SourceKind = DependencySourceKind.Local };

        /// <summary>
        /// Creates a <see cref="DependencySource"/> that represents a build server artifact dependency that has been restored,
        /// and that exists under the 'dependencies' directory.
        /// </summary>
        public static DependencySource CreateRestoredDependency(
            BuildContext context,
            DependencyDefinition dependencyDefinition,
            DependencyConfigurationOrigin origin,
            (TeamCityClient TeamCity, BuildConfiguration BuildConfiguration, ImmutableDictionary<string, string> ArtifactRules)? teamCityEmulation )
        {
            if ( teamCityEmulation != null )
            {
                var buildTypeId = dependencyDefinition.CiConfiguration.BuildTypes[teamCityEmulation.Value.BuildConfiguration];

                var latestCiBuildId = teamCityEmulation.Value.TeamCity.GetLatestBuildId( buildTypeId, dependencyDefinition.Branch, true );

                if ( latestCiBuildId == null || latestCiBuildId.BuildTypeId == null )
                {
                    throw new InvalidOperationException( $"Cannot find latest build of '{buildTypeId}'." );
                }

                if ( !DependenciesHelper.DownloadBuild(
                        context,
                        teamCityEmulation.Value.TeamCity,
                        dependencyDefinition.Name,
                        latestCiBuildId.BuildTypeId,
                        latestCiBuildId.BuildNumber,
                        out _,
                        teamCityEmulation.Value.ArtifactRules ) )
                {
                    throw new InvalidOperationException( $"Failed to download '{latestCiBuildId.BuildTypeId}' build #{latestCiBuildId.BuildNumber}" );
                }
            }

            var path = Path.Combine( context.RepoDirectory, "dependencies", dependencyDefinition.Name, $"{dependencyDefinition.Name}.version.props" );
            var document = XDocument.Load( path );

            var buildNumber = document.Root!.XPathSelectElement( $"/Project/PropertyGroup/{dependencyDefinition.NameWithoutDot}BuildNumber" )?.Value;

            if ( buildNumber == null )
            {
                throw new InvalidOperationException( $"The file '{path}' does not have a property {dependencyDefinition.NameWithoutDot}BuildNumber" );
            }

            var buildType = document.Root!.XPathSelectElement( $"/Project/PropertyGroup/{dependencyDefinition.NameWithoutDot}BuildType" )?.Value;

            if ( buildType == null )
            {
                throw new InvalidOperationException( $"The file '{path}' does not have a property {dependencyDefinition.NameWithoutDot}BuildType" );
            }

            var buildId = new CiBuildId( int.Parse( buildNumber, CultureInfo.InvariantCulture ), buildType );

            return CreateRestoredDependency( buildId, origin );
        }

        public static DependencySource CreateRestoredDependency( CiBuildId buildId, DependencyConfigurationOrigin origin )
            => new() { Origin = origin, SourceKind = DependencySourceKind.RestoredDependency, BuildServerSource = buildId };

        public static DependencySource CreateFeed( string? version, DependencyConfigurationOrigin origin )
            => new() { Origin = origin, SourceKind = DependencySourceKind.Feed, Version = version };

        public static DependencySource CreateBuildServerSource( ICiBuildSpec source, DependencyConfigurationOrigin origin )
            => new() { Origin = origin, SourceKind = DependencySourceKind.BuildServer, BuildServerSource = source };

        public override string ToString()
        {
            switch ( this.SourceKind )
            {
                case DependencySourceKind.BuildServer or DependencySourceKind.RestoredDependency:
                    return $"{this.SourceKind}, {this.BuildServerSource}, Origin={this.Origin}";

                case DependencySourceKind.Local:
                    {
                        return $"{this.SourceKind}, Origin={this.Origin}";
                    }

                case DependencySourceKind.Feed:
                    return $"{this.SourceKind}, {this.Version}, Origin={this.Origin}";

                default:
                    return "<Error>";
            }
        }
    }
}