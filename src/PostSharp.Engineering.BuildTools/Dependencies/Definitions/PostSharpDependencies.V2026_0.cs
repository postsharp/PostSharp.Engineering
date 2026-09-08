// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.TeamCity;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Tools.TeamCity;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

public static partial class PostSharpDependencies
{
    // ReSharper disable once InconsistentNaming

    [PublicAPI]
    public static class V2026_0
    {
        public static ProductFamily Family { get; } =
            new( _projectName, "2026.0", DevelopmentDependencies.Family )
            {
                GitHubAppConnectionId = GitHubAppConnections.PostSharp,

                // Changes flow from the 2024.0 line into this one. Declaring the upstream is what generates the
                // 'Upstream Merge' build configuration and the 'Check pending upstream changes' step on the
                // publishing configurations.
                UpstreamProductFamily = V2024_0.Family
            };

        private static readonly TeamCityProjectId _teamCityProjectId = new(
            $"PostSharpGitHub_{_projectName}{Family.VersionWithoutDots}",
            "PostSharpGitHub" );

        private static readonly string _distributionBuildId = $"{_teamCityProjectId}_BuildSignedDistribution";

        /// <summary>
        /// The repository as it is built, from the development branch. This is the definition the repository's own
        /// <see cref="Build.Model.Product"/> is constructed from; <see cref="PostSharp"/> is the one its consumers use.
        /// </summary>
        /// <remarks>
        /// Both definitions belong to the same TeamCity project, which is why they are told apart by name rather than
        /// by project -- see <see cref="ProductFamily.Register"/>.
        /// </remarks>
        public static DependencyDefinition PostSharpProduct { get; } = new(
            Family,
            _projectName,
            $"develop/{Family.Version}",
            $"release/{Family.Version}",
            new GitHubRepository( _projectName, "postsharp" ),
            TeamCityHelper.CreateConfiguration( _teamCityProjectId, vcsRootId: _teamCityProjectId.Id ) )
        {
            GenerateSnapshotDependency = false,
            Dependencies = [DevelopmentDependencies.PostSharpEngineering],

            // The packages this repository builds. The default is the product name followed by ".*", which would claim
            // PostSharp.Engineering.*: package source mapping would then look for the engineering packages in the
            // artifact directory, where they are not, and a restore against the generated nuget.config fails NU1101.
            // PostSharp.Settings.* is built by UserInterface and is not part of the 2024.0 set.
            PackagePatterns = ["PostSharp", "PostSharp.Redist", "PostSharp.Compiler.*", "PostSharp.Patterns.*", "PostSharp.Settings.*"]
        };

        /// <summary>
        /// The packages this repository publishes, as its consumers resolve them: from the signed distribution built on
        /// the release branch. The name is load-bearing -- it is what makes the version property
        /// <c>PostSharpPackageVersion</c>, which consumers reference by that name.
        /// </summary>
        public static DependencyDefinition PostSharp { get; } = new(
            Family,
            "PostSharpPackage",
            $"refs/heads/release/{Family.Version}",
            null,
            new GitHubRepository( _projectName, _projectName ),
            new CiProjectConfiguration(
                _teamCityProjectId,
                new ConfigurationSpecific<string>( "not-used", _distributionBuildId, "not-used" ),
                null,
                null,
                EnvironmentVariableNames.TeamCityToken,
                TeamCityHelper.TeamCityCloudUrl ),
            false )
        {
            EngineeringDirectory = @"Build\Distribution\eng",
            PackagePatterns = ["PostSharp", "PostSharp.Redist", "PostSharp.Compiler.*", "PostSharp.Patterns.*", "PostSharp.Settings.*"],
            AutoUpdateVersion = false
        };
    }
}