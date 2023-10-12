// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Utilities;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Git;

internal class UpstreamCheckCommand : BaseCommand<UpstreamCheckSettings>
{
    protected override bool ExecuteCore( BuildContext context, UpstreamCheckSettings settings )
    {
        context.Console.WriteHeading( "Checking for pending upstream changes" );
        
        if ( !TryCheckPendingMerges( context, settings ) )
        {
            return false;
        }
        
        if ( !TryCheckUnmergedCommits( context, settings ) )
        {
            return false;
        }

        context.Console.WriteSuccess( settings.Force ? "Pending upstream changes check completed." : "No pending upstream changes found." );

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
                context.Console.WriteWarning(
                    $"The '{context.Product.ProductName}' upstream product version '{upstreamProductFamily.Version}' is not configured. Assuming it didn't exists in this family version." );

                break;
            }
            
            var upstreamBranch = upstreamDependencyDefinition.Branch;

            if ( !GitHelper.TryFetch( context, upstreamBranch ) )
            {
                return false;
            }

            var remoteUpstreamBranch = $"remotes/origin/{upstreamBranch}";
            
            if ( !GitHelper.TryGetCommitsCount( context, "HEAD", remoteUpstreamBranch, upstreamProductFamily, out var commitsCount ) )
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
        var pendingBranchesExist = false;

        while ( productFamily != null )
        {
            context.Console.WriteMessage( $"Checking '{context.Product.ProductName}' product version '{productFamily.Version}' for pending merge branches." );
            
            var filter = $"merge/{productFamily.Version}/*";
            
            if ( !GitHelper.TryGetRemoteReferences( context, settings, filter, out var references ) )
            {
                return false;
            }

            if ( references.Length > 0 )
            {
                MergeHelper.ExplainUnmergedBranches(
                    context.Console,
                    references.Select( r => r.Reference ),
                    settings.Force );

                pendingBranchesExist = true;
            }

            productFamily = productFamily.UpstreamProductFamily;
        }

        if ( settings.Force )
        {
            return true;
        }

        if ( pendingBranchesExist )
        {
            return false;
        }
        else
        {
            return true;
        }
    }
}