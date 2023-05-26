// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PostSharp.Engineering.BuildTools.Git;

internal class DownstreamMergeCommand : BaseCommand<DownstreamMergeSettings>
{
    protected override bool ExecuteCore( BuildContext context, DownstreamMergeSettings settings )
    {
        if ( context.Product.VcsProvider == null )
        {
            context.Console.WriteError( "The VcsProvider is not initialized." );

            return false;
        }

        var sourceBranch = context.Product.VcsProvider.DefaultBranch;
        var downstreamBranch = context.Product.VcsProvider.DownstreamBranch;

        context.Console.WriteHeading( $"Executing downstream merge from '{sourceBranch}' branch to '{downstreamBranch}' branch." );

        if ( context.Branch != sourceBranch )
        {
            context.Console.WriteError(
                $"Downstream merge can only be executed on the default branch ('{sourceBranch}'). The current branch is '{context.Branch}'." );

            return false;
        }

        if ( !VcsHelper.TryGetCurrentCommitHash( context, out var sourceCommitHash ) )
        {
            return false;
        }

        if ( !VcsHelper.TryCheckoutAndPull( context, downstreamBranch ) )
        {
            return false;
        }

        if ( !VcsHelper.TryGetCurrentCommitHash( context, out var downstreamHeadCommitHashBeforeMerge ) )
        {
            return false;
        }

        // TODO: Set the upstream main version in PostSharp.Engineering 2023.1
        var targetBranch = $"merge/2023-0-{sourceCommitHash}";

        // If the targetBranch exists already, use it. Otherwise, create it.
        if ( VcsHelper.TryCheckoutAndPull( context, targetBranch ) )
        {
            context.Console.WriteMessage( $"The '{targetBranch}' target branch already exists." );
        }
        else
        {
            context.Console.WriteMessage( $"Creating the '{targetBranch}' target branch already exists." );

            if ( !VcsHelper.TryCreateBranch( context, targetBranch ) )
            {
                return false;
            }

            // Push the new branch now to avoid issues when the DownstreamMergeCommand
            // is executed again with the same upstream changes.
            if ( !VcsHelper.TryPush( context, settings ) )
            {
                return false;
            }
        }

        // If the automated merge fails, try to resolve expected conflicts.
        if ( VcsHelper.TryMerge( context, sourceBranch, targetBranch ) )
        {
            if ( !VcsHelper.TryGetCurrentCommitHash( context, out var downstreamHeadCommitHashAfterMerge ) )
            {
                return false;
            }

            if ( downstreamHeadCommitHashAfterMerge == downstreamHeadCommitHashBeforeMerge )
            {
                context.Console.WriteSuccess( $"There is nothing to merge from '{sourceBranch}' branch to '{downstreamBranch}' branch." );

                return true;
            }
        }
        else
        {
            if ( !VcsHelper.TryGetStatus( context, settings, context.RepoDirectory, out var status ) )
            {
                return false;
            }

            var solvableConflicts = new Dictionary<string, string>();

            void AddSolvableConflict( string path )
            {
                var unixStylePath = path.Replace( Path.DirectorySeparatorChar, '/' );
                solvableConflicts.Add( $"UU {unixStylePath}", unixStylePath );
            }

            AddSolvableConflict( context.Product.MainVersionFilePath );
            AddSolvableConflict( context.Product.BumpInfoFilePath );

            foreach ( var s in status )
            {
                if ( solvableConflicts.TryGetValue( s, out var fileToResolve ) )
                {
                    if ( !VcsHelper.TryResolveUsingOurs( context, fileToResolve ) )
                    {
                        return false;
                    }
                }
            }

            // If not all conflicts were expected, git commit fails here.
            if ( !VcsHelper.TryCommit( context ) )
            {
                context.Console.WriteError(
                    $"Merge conflicts need to be resolved manually. Merge '{sourceBranch}' branch to '{targetBranch}' branch, Then create a pull request to '{downstreamBranch}' branch or execute this command again." );

                return false;
            }
        }

        if ( !VcsHelper.TryPush( context, settings ) )
        {
            return false;
        }

        if ( !VcsHelper.TryGetRemoteUrl( context, out var remoteUrl ) )
        {
            return false;
        }

        var pullRequestTitle = $"Downstream merge from '{sourceBranch}' branch";
        Task<bool> newPullRequestTask;
        
        if ( AzureDevOpsRepoUrlParser.TryParse( remoteUrl, out var baseUrl, out var projectName, out var repoName ) )
        {
            newPullRequestTask = AzureDevOpsHelper.TryCreatePullRequest(
                context.Console,
                baseUrl,
                projectName,
                repoName,
                targetBranch,
                downstreamBranch,
                pullRequestTitle );
        }
        else if ( GitHubRepoUrlParser.TryParse( remoteUrl, out var repoOwner, out repoName ) )
        {
            newPullRequestTask = GitHubHelper.TryCreatePullRequest(
                context.Console,
                repoOwner,
                repoName,
                targetBranch,
                downstreamBranch,
                pullRequestTitle );
        }
        else
        {
            context.Console.WriteError( $"Unknown VCS or unexpected repo URL format. Repo URL: '{remoteUrl}'." );

            return false;
        }

        try
        {
            if ( !newPullRequestTask.ConfigureAwait( false ).GetAwaiter().GetResult() )
            {
                return false;
            }
        }
        catch ( Exception e )
        {
            context.Console.WriteError( e.ToString() );

            return false;
        }

        return true;
    }
}