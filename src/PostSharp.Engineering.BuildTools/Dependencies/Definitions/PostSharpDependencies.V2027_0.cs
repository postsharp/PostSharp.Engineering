// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.TeamCity;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Tools.TeamCity;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

public static partial class PostSharpDependencies
{
    // ReSharper disable once InconsistentNaming

    [PublicAPI]
    public static class V2027_0
    {
        public static ProductFamily Family { get; } =
            new( _projectName, "2027.0", DevelopmentDependencies.Family )
            {
                GitHubAppConnectionId = GitHubAppConnections.PostSharp,
                UpstreamProductFamily = V2026_0.Family
            };

        private static TeamCityProjectId GetProjectId( string dependencyName )
            => TeamCityHelper.GetProjectIdWithParentProjectId( $"{dependencyName} {Family.Version}", _parentProjectId );

        /// <summary>
        /// A repository of this line: it builds from the development branch, publishes from the release branch, and
        /// owns a TeamCity project and a VCS root named after itself and the version.
        /// </summary>
        private class PostSharpDependencyDefinition : DependencyDefinition
        {
            public PostSharpDependencyDefinition( string dependencyName )
                : base(
                    Family,
                    dependencyName,
                    $"develop/{Family.Version}",
                    $"release/{Family.Version}",
                    new GitHubRepository( dependencyName, _projectName ),
                    TeamCityHelper.CreateConfiguration( GetProjectId( dependencyName ), vcsRootId: GetProjectId( dependencyName ).Id ) ) { }
        }

        /// <summary>The compiler and the pattern libraries.</summary>
        public static DependencyDefinition PostSharp { get; } = new PostSharpDependencyDefinition( _projectName )
        {
            GenerateSnapshotDependency = false,
            Dependencies = [DevelopmentDependencies.PostSharpEngineering],
            PackagePatterns = ["PostSharp", "PostSharp.Redist", "PostSharp.Compiler.*", "PostSharp.Patterns.*", "PostSharp.Settings.*"],
            AutoUpdateVersion = false
        };

        /// <summary>The documentation site, which documents this line and is built against its packages.</summary>
        public static DependencyDefinition PostSharpDocumentation { get; } =
            new PostSharpDependencyDefinition( $"{_projectName}.Documentation" )
            {
                Dependencies =
                [
                    DevelopmentDependencies.PostSharpEngineering.ToDependency(),
                    PostSharp.ToDependency(
                        new ConfigurationSpecific<BuildConfiguration>(
                            BuildConfiguration.Public,
                            BuildConfiguration.Public,
                            BuildConfiguration.Public ) )
                ]
            };
    }
}
