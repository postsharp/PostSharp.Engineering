﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using System;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public partial class TestDependencies
{
    private const string _projectName = "Engineering";
    
    private static VcsRepository CreateEngineeringVcsRepository( string name, VcsProvider provider )
    {
        switch ( provider )
        {
            case VcsProvider.AzureDevOps:
                return new AzureDevOpsRepository( _projectName, name );
            
            case VcsProvider.GitHub:
                return new GitHubRepository( name );
            
            default:
                throw new InvalidOperationException( $"Unknown VCS provider: \"{provider}\"" );
        }
    }
}