// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
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
            DependencyConfigurationOrigin origin )
        {
            var path = Path.Combine( context.RepoDirectory, "dependencies", dependencyDefinition.Name, $"{dependencyDefinition.Name}.version.props" );
            var document = XDocument.Load( path );

            var buildNumber = document.Root!.XPathSelectElement( $"/Project/PropertyGroup/{dependencyDefinition.NameWithoutDot}BuildNumber" )?.Value;
            var buildType = document.Root!.XPathSelectElement( $"/Project/PropertyGroup/{dependencyDefinition.NameWithoutDot}BuildType" )?.Value;

            CiBuildId? buildId;

            if ( string.IsNullOrEmpty( buildNumber ) || string.IsNullOrEmpty( buildType ) )
            {
                buildId = null;
            }
            else
            {
                buildId = new CiBuildId( int.Parse( buildNumber, CultureInfo.InvariantCulture ), buildType );
            }

            return CreateRestoredDependency( buildId, origin );
        }

        public static DependencySource CreateRestoredDependency( CiBuildId? buildId, DependencyConfigurationOrigin origin )
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