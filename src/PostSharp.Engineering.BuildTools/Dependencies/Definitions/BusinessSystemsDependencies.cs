// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static class BusinessSystemsDependencies
{
    private class BusinessSystemsDependencyDefinition : DependencyDefinition
    {
        public BusinessSystemsDependencyDefinition( string dependencyName )
            : base(
                Family,
                dependencyName,
                "master",
                null,
                VcsProvider.AzureRepos,
                "Business%20Systems",
                TeamCityHelper.CreateConfiguration( TeamCityHelper.GetProjectId( dependencyName, "Websites And Business Systems" ), "webdeploy" ),
                false ) { }
    }

    public static ProductFamily Family { get; } = new( "1.0", DevelopmentDependencies.Family );

    public static DependencyDefinition BusinessSystems { get; } = new BusinessSystemsDependencyDefinition( "BusinessSystems" );

    public static DependencyDefinition HelpBrowser { get; } = new BusinessSystemsDependencyDefinition( "HelpBrowser" );

    public static DependencyDefinition PostSharpWeb { get; } = new BusinessSystemsDependencyDefinition( "PostSharpWeb" );
}