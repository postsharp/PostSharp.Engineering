// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static class InternalDependencies
{
    private const string _devBranch = "master";
    private const string? _releaseBranch = null;

    public static ProductFamily Family { get; } = new( "1.0" );

    public static DependencyDefinition BusinessSystems { get; } = new(
        Family,
        "BusinessSystems",
        _devBranch,
        _releaseBranch,
        VcsProvider.AzureRepos,
        "WebsitesAndBusinessSystems",
        false );

    public static DependencyDefinition HelpBrowser { get; } = new(
        Family,
        "HelpBrowser",
        _devBranch,
        _releaseBranch,
        VcsProvider.AzureRepos,
        "WebsitesAndBusinessSystems",
        false );

    public static DependencyDefinition PostSharpWeb { get; } = new(
        Family,
        "PostSharpWeb",
        _devBranch,
        _releaseBranch,
        VcsProvider.AzureRepos,
        "WebsitesAndBusinessSystems",
        false );
}