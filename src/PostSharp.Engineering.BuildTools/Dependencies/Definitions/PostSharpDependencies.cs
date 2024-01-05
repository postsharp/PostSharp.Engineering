﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static partial class PostSharpDependencies
{
    private const string _projectName = "PostSharp";
    
    private class PostSharpDocumentationDependencyDefinition : DependencyDefinition
    {
        public PostSharpDocumentationDependencyDefinition(
            string dependencyName,
            VcsProvider vcsProvider )
            : base(
                DocumentationFamily,
                dependencyName,
                "dev",
                "master",
                new GitHubRepository( dependencyName ),
                TeamCityHelper.CreateConfiguration(
                    TeamCityHelper.GetProjectId( dependencyName, _projectName ),
                    "caravela04",
                    false,
                    isCloudInstance: false ),
                false ) { }
    }

    public static ProductFamily DocumentationFamily { get; } = new( "PostSharp.Documentation", "1.0", DevelopmentDependencies.Family );

    public static DependencyDefinition PostSharpDocumentation { get; } = new PostSharpDocumentationDependencyDefinition(
        "PostSharp.Documentation",
        VcsProvider.GitHub );
}