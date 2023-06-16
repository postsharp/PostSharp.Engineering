// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static class DevelopmentDependencies
{
    public static ProductFamily Family { get; } = new( "Engineering", "2023.2" );

    public static DependencyDefinition PostSharpEngineering { get; } = new(
        Family,
        "PostSharp.Engineering",
        $"develop/{Family.Version}",
        $"release/{Family.Version}",
        new GitHubRepository( "PostSharp.Engineering" ),
        TeamCityHelper.CreateConfiguration(
            TeamCityHelper.GetProjectId( "PostSharp.Engineering", Family.Name ),
            "caravela04cloud",
            true,
            BuildConfiguration.Debug,
            BuildConfiguration.Debug,
            BuildConfiguration.Debug ) ) { GenerateSnapshotDependency = false };
}