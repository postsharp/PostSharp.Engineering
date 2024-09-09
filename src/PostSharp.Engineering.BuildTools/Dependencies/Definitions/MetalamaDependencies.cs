// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using System;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public partial class MetalamaDependencies
{
    private const string _projectName = "Metalama";

    private static VcsRepository CreateMetalamaVcsRepository( string name, VcsProvider provider, string? defaultBranchParameter )
    {
        switch ( provider )
        {
            case VcsProvider.AzureDevOps:
                return new AzureDevOpsRepository( _projectName, name, defaultBranchParameter: defaultBranchParameter );
            
            case VcsProvider.GitHub:
                return new GitHubRepository( name, defaultBranchParameter: defaultBranchParameter );
            
            default:
                throw new InvalidOperationException( $"Unknown VCS provider: \"{provider}\"" );
        }
    }
}