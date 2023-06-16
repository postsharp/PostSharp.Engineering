﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static class TemplateDependencies
{
    private class TemplateDependencyDefinition : DependencyDefinition
    {
        public TemplateDependencyDefinition(
            string dependencyName,
            VcsProvider vcsProvider,
            string? vcsProjectName,
            bool isVersioned = true )
            : base(
                Family,
                dependencyName,
                $"develop/{Family.Version}",
                $"release/{Family.Version}",
                new GitHubRepository( dependencyName ),
                TeamCityHelper.CreateConfiguration( TeamCityHelper.GetProjectId( dependencyName, "NONE" ), "none", isVersioned ),
                isVersioned ) { }
    }

    public static ProductFamily Family { get; } = new( "Template", "2023.0", DevelopmentDependencies.Family );
    
    // This is only used from the project template.
    public static DependencyDefinition MyProduct { get; } =
        new TemplateDependencyDefinition( "PostSharp.Engineering.ProjectTemplate", VcsProvider.GitHub, "NONE" );
}