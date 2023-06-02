// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static partial class PostSharpDependencies
{
    private class PostSharpDependencyDefinition : DependencyDefinition
    {
        public PostSharpDependencyDefinition(
            string dependencyName,
            VcsProvider vcsProvider,
            string? vcsProjectName,
            bool isVersioned = true )
            : base(
                Family,
                dependencyName,
                $"release/{Family.Version}",
                null,
                vcsProvider,
                vcsProjectName,
                TeamCityHelper.CreateConfiguration(
                    TeamCityHelper.GetProjectId( dependencyName, "PostSharp" ),
                    "caravela04",
                    isVersioned,
                    isCloudInstance: false ),
                isVersioned ) { }
    }
    
    public static ProductFamily Family { get; } = new( "1.0", DevelopmentDependencies.Family );

    public static DependencyDefinition PostSharpDocumentation { get; } = new PostSharpDependencyDefinition(
        "PostSharp.Documentation",
        VcsProvider.GitHub,
        null,
        false );
}