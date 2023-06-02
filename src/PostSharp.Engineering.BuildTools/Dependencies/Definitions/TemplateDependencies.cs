﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
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
                vcsProvider,
                vcsProjectName,
                TeamCityHelper.CreateConfiguration( TeamCityHelper.GetProjectId( dependencyName, "NONE" ), isVersioned ),
                isVersioned ) { }
    }
    
    public static ProductFamily Family { get; } = new( "2023.0" );
    
    // This is only used from the project template.
    public static DependencyDefinition MyProduct { get; } =
        new TemplateDependencyDefinition( "PostSharp.Engineering.ProjectTemplate", VcsProvider.GitHub, "NONE" );
}