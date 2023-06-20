﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PostSharp.Engineering.BuildTools.Git;

internal class SetBranchPoliciesCommand : BaseCommand<SetBranchPoliciesSettings>
{
    protected override bool ExecuteCore( BuildContext context, SetBranchPoliciesSettings settings )
    {
        context.Console.WriteHeading( "Setting branch policies" );
        
        if ( !GitHelper.TryGetRemoteUrl( context, out var remoteUrl ) )
        {
            return false;
        }
        
        if ( AzureDevOpsRepository.TryParse( remoteUrl, out var azureDevOpsRepository ) )
        {
            if ( !AzureDevOpsHelper.TrySetBranchPolicies( context, azureDevOpsRepository, settings.Dry ) )
            {
                return false;
            }
        }
        else if ( GitHubRepository.TryParse( remoteUrl, out var gitHubRepository ) )
        {
            throw new NotImplementedException();
        }
        else
        {
            context.Console.WriteError( $"Unknown VCS or unexpected repo URL format. Repo URL: '{remoteUrl}'." );

            return false;
        }

        context.Console.WriteSuccess( settings.Dry ? "Dry run of branch policies setting succeeded." : "Branch policies set successfully." );

        return true;
    }

    private static bool TryCheckUnmergedCommits( BuildContext context, UpstreamCheckSettings settings )
    {
        var upstreamProductFamily = context.Product.ProductFamily.UpstreamProductFamily;

        while ( upstreamProductFamily != null )
        {
            context.Console.WriteMessage( $"Checking '{context.Product.ProductName}' product version '{upstreamProductFamily.Version}' for unmerged changes." );
            
            if ( !upstreamProductFamily.TryGetDependencyDefinition( context.Product.ProductName, out var upstreamDependencyDefinition ) )
            {
                context.Console.WriteError(
                    $"The '{context.Product.ProductName}' upstream product version '{upstreamProductFamily.Version}' is not configured." );

                return false;
            }
            
            var upstreamBranch = upstreamDependencyDefinition.Branch;

            if ( !GitHelper.TryFetch( context, upstreamBranch, out _ ) )
            {
                return false;
            }
            
            if ( !GitHelper.TryGetCommitsCount( context, "HEAD", upstreamBranch, out var commitsCount ) )
            {
                return false;
            }

            if ( commitsCount > 0 )
            {
                var message =
                    $"There are unmerged changes from the '{context.Product.ProductName}' upstream product version '{upstreamProductFamily.Version}'.";
                
                if ( settings.Force )
                {
                    context.Console.WriteWarning( $"{message} Ignoring." );
                }
                else
                {
                    context.Console.WriteError(
                        $"{message} Run the related downstream merge or use --force." );

                    return false;
                }
            }

            upstreamProductFamily = upstreamProductFamily.UpstreamProductFamily;
        }

        return true;
    }

    private static bool TryCheckPendingMerges( BuildContext context, UpstreamCheckSettings settings )
    {
        var productFamily = context.Product.ProductFamily;

        while ( productFamily != null )
        {
            context.Console.WriteMessage( $"Checking '{context.Product.ProductName}' product version '{productFamily.Version}' for pending merge branches." );
            
            var filter = $"merge/{productFamily.Version}/*";
            
            if ( !GitHelper.TryGetRemoteBranchesCount( context, settings, filter, out var count ) )
            {
                return false;
            }

            if ( count > 0 )
            {
                var message =
                    $"There are '{filter}' branches in the repository of the '{context.Product.ProductName}' product version '{productFamily.Version}'.";
                
                if ( settings.Force )
                {
                    context.Console.WriteWarning( $"{message} Ignoring." );
                }
                else
                {
                    context.Console.WriteError(
                        $"{message} Finish the related merges, delete the branches, or use --force." );

                    return false;
                }
            }

            productFamily = productFamily.UpstreamProductFamily;
        }

        return true;
    }
}