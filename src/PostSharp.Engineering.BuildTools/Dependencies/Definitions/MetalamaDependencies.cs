// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using System;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public partial class MetalamaDependencies
{
    private static string? GetDefaultVcsProjectName( VcsProvider vcsProvider )
        => vcsProvider.Name switch
        {
            VcsProviderName.GitHub => null,
            VcsProviderName.AzureDevOps => "Metalama",
            _ => throw new InvalidOperationException( $"Unknown VCS provider name: '{vcsProvider.Name}'" )
        };
}