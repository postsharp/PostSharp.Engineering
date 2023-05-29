// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

        var targetBranch = $"merge/{context.DownstreamBranchVersion}/{context.CurrentBranchVersion}-{sourceCommitHash}";

        // If the targetBranch exists already, use it. Otherwise, create it.
        if ( VcsHelper.TryCheckoutAndPull( context, targetBranch ) )
        {
            context.Console.WriteMessage( $"The '{targetBranch}' target branch already exists. Let's use it." );
        }
        else
        {
            context.Console.WriteMessage( $"The '{targetBranch}' target branch doesn't exits. Let's create it." );

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
            context.Console.WriteMessage( "The git merge failed. Trying to resolve conflicts." );

            if ( !VcsHelper.TryGetStatus( context, settings, context.RepoDirectory, out var statuses ) )
            {
                return false;
            }

            // Try to resolve conflicts
            if ( statuses.Length > 0 )
            {
                var solvableConflicts = new HashSet<string>();

                void AddSolvableConflict( string path )
                {
                    var unixStylePath = path.Replace( Path.DirectorySeparatorChar, '/' );
                    solvableConflicts.Add( unixStylePath );
                }

                AddSolvableConflict( context.Product.MainVersionFilePath );
                AddSolvableConflict( context.Product.VersionsFilePath );
                AddSolvableConflict( context.Product.BumpInfoFilePath );

                foreach ( var status in statuses.Select( s => s.Split( ' ', 2, StringSplitOptions.TrimEntries ) ) )
                {
                    var fileToResolve = status[1];

                    // The global.json can be found in various folders. Eg. in Metalama.Try.
                    if ( solvableConflicts.Contains( fileToResolve )
                         || fileToResolve == "global.json"
                         || fileToResolve.EndsWith( "/global.json", StringComparison.InvariantCulture ) )
                    {
                        if ( !VcsHelper.TryResolveUsingOurs( context, fileToResolve ) )
                        {
                            return false;
                        }
                    }
                }
            }

            // If not all conflicts were expected, git commit fails here.
            if ( !VcsHelper.TryCommitMerge( context ) )
            {
                context.Console.WriteError(
                    $"Merge conflicts need to be resolved manually. Merge '{sourceBranch}' branch to '{targetBranch}' branch. Then create a pull request to '{downstreamBranch}' branch or execute this command again." );

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
        Task<string?> newPullRequestTask;

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

        string? pullRequestUrl;

        try
        {
            pullRequestUrl = newPullRequestTask.ConfigureAwait( false ).GetAwaiter().GetResult();

            if ( pullRequestUrl == null )
            {
                return false;
            }
        }
        catch ( Exception e )
        {
            context.Console.WriteError( e.ToString() );

            return false;
        }

        var buildTypeId = string.Format(
            CultureInfo.InvariantCulture,
            context.Product.DependencyDefinition.DownstreamBuildTypeFormat,
            context.DownstreamBranchVersionWithoutDot );

        context.Console.WriteMessage( $"Scheduling build {buildTypeId} on {targetBranch} branch." );

        var teamCityToken = Environment.GetEnvironmentVariable( "TEAMCITY_CLOUD_TOKEN" );

        if ( string.IsNullOrEmpty( teamCityToken ) )
        {
            context.Console.WriteError( "The TEAMCITY_CLOUD_TOKEN environment variable is not defined." );

            return false;
        }

        var tc = new TeamCityClient( teamCityToken );

        var buildId = tc.ScheduleBuild(
            context.Console,
            buildTypeId,
            $"Triggered by PostSharp.Engineering for downstream merge from '{sourceBranch}' branch to auto-complete pull request {pullRequestUrl}",
            targetBranch,
            TeamCityHelper.TeamcityCloudApiBuildQueueUri );

        if ( buildId == null )
        {
            return false;
        }

        context.Console.WriteSuccess( $"Scheduled build https://postsharp.teamcity.com/viewLog.html?buildId={buildId}" );

        return true;
    }
}