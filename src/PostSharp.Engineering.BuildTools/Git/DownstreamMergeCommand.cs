// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

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

internal class DownstreamMergeCommand : BaseCommand<DownstreamMergeSettings>
{
    protected override bool ExecuteCore( BuildContext context, DownstreamMergeSettings settings )
    {
        // When on TeamCity, Git user credentials are set to TeamCity.
        if ( TeamCityHelper.IsTeamCityBuild( settings ) )
        {
            if ( !TeamCityHelper.TrySetGitIdentityCredentials( context ) )
            {
                return false;
            }
        }
        
        if ( !GitHelper.TryGetStatus( context, context.RepoDirectory, out var statuses ) )
        {
            return false;
        }

        if ( statuses.Length > 0 )
        {
            context.Console.WriteError( "The repository needs to be clean before running the downstream merge." );

            return false;
        }

        var sourceProductFamily = context.Product.ProductFamily;
        var downstreamProductFamily = context.Product.ProductFamily.DownstreamProductFamily;

        if ( downstreamProductFamily == null )
        {
            context.Console.WriteWarning(
                $"The downstream version product family for '{context.Product.ProductFamily.Version}' is not configured. Skipping downstream merge." );

            return true;
        }

        var sourceBranch = context.Product.DependencyDefinition.Branch;

        if ( !downstreamProductFamily.TryGetDependencyDefinition( context.Product.ProductName, out var downstreamDependencyDefinition ) )
        {
            context.Console.WriteError(
                $"The '{context.Product.ProductName}' downstream product version '{downstreamProductFamily.Version}' is not configured." );

            return false;
        }

        var downstreamBranch = downstreamDependencyDefinition.Branch;

        if ( context.Branch != sourceBranch )
        {
            context.Console.WriteError(
                $"Downstream merge can only be executed on the development branch ('{sourceBranch}'). The current branch is '{context.Branch}'." );

            return false;
        }

        context.Console.WriteHeading( $"Executing downstream merge from '{sourceBranch}' branch to '{downstreamBranch}' branch" );

        if ( !GitHelper.TryGetCurrentCommitHash( context, out var sourceCommitHash ) )
        {
            return false;
        }

        context.Console.WriteImportantMessage( $"Pulling changes from '{downstreamBranch}' downstream branch" );

        if ( !GitHelper.TryCheckoutAndPull( context, downstreamBranch ) )
        {
            return false;
        }

        if ( !GitHelper.TryGetCommitsCount( context, "HEAD", sourceBranch, sourceProductFamily, out var commitsCount ) )
        {
            return false;
        }

        if ( commitsCount < 0 )
        {
            throw new InvalidOperationException( $"Invalid commits count: {commitsCount}" );
        }

        if ( commitsCount == 0 )
        {
            context.Console.WriteSuccess( $"There are no commits to merge from '{sourceBranch}' branch to '{downstreamBranch}' branch." );

            return true;
        }

        context.Console.WriteImportantMessage( $"There are {commitsCount} commits to merge from '{sourceBranch}' branch to '{downstreamBranch}' branch." );

        var targetBranch = $"merge/{downstreamProductFamily.Version}/{context.Product.ProductFamily.Version}-{sourceCommitHash}";
        
        context.Console.WriteMessage( $"Checking '{context.Product.ProductName}' product version '{downstreamProductFamily.Version}' for pending merge branches." );
            
        var filter = $"merge/{downstreamProductFamily.Version}/*";
            
        if ( !GitHelper.TryGetRemoteReferences( context, settings, filter, out var references ) )
        {
            return false;
        }

        var targetBranchReference = $"refs/heads/{targetBranch}";

        var targetBranchExistsRemotely = references.Any( r => r.Reference == targetBranchReference );

        var formerTargetBranchReferences = references.Where( r => r.Reference != targetBranchReference ).ToArray();

        if ( formerTargetBranchReferences.Length > 0 && !settings.Force )
        {
            MergeHelper.ExplainUnmergedBranches(
                context.Console,
                formerTargetBranchReferences.Select( r => r.Reference ),
                settings.Force,
                targetBranchExistsRemotely
                    ? $"Until a new commit is pushed to the '{sourceBranch}' source branch, there's no need to delete the '{targetBranch}' target branch, as it will be reused next time the downstream merge is run."
                    : null );

            if ( !settings.Force )
            {
                return false;
            }
        }

        bool targetBranchExists;

        if ( targetBranchExistsRemotely )
        {
            targetBranchExists = true;
        }
        else
        {
            if ( !GitHelper.TryGetCurrentCommitHash( context, targetBranch, out var targetBranchCurrentCommitHash ) )
            {
                return false;
            }

            targetBranchExists = targetBranchCurrentCommitHash != null;
        }

        if ( targetBranchExists )
        {
            context.Console.WriteImportantMessage( $"The '{targetBranch}' target branch already exists. Let's use it." );
            
            if ( !GitHelper.TryCheckoutAndPull( context, targetBranch ) )
            {
                return false;
            }
        }
        else
        {
            context.Console.WriteImportantMessage( $"The '{targetBranch}' target branch doesn't exits. Let's create it." );

            if ( !GitHelper.TryCreateBranch( context, targetBranch ) )
            {
                return false;
            }

            context.Console.WriteImportantMessage( $"The '{targetBranch}' target was created." );
        }
        
        // Push the branch now to avoid issues when the DownstreamMergeCommand
        // is executed again with the same upstream changes
        // or when developers are required to resolve conflicts.
        if ( !GitHelper.TryPush( context, settings ) )
        {
            return false;
        }

        if ( !TryMerge( context, settings, sourceBranch, targetBranch, downstreamBranch, out var areChangesPending ) )
        {
            return false;
        }

        if ( !areChangesPending )
        {
            // This shouldn't happen often - just when the merge conflict is solved without using the merge branch prepared by the tool.
            context.Console.WriteSuccess( $"There is nothing to merge from '{sourceBranch}' branch to '{downstreamBranch}' branch." );

            return true;
        }

        if ( !TryCreatePullRequest( context, targetBranch, downstreamBranch, sourceBranch, out var pullRequestUrl ) )
        {
            return false;
        }

        var buildTypeId = downstreamDependencyDefinition.CiConfiguration.PullRequestStatusCheckBuildType;
        string? buildUrl = null;

        if ( buildTypeId == null )
        {
            context.Console.WriteImportantMessage( "Build scheduling is not required." );
        }
        else
        {
            if ( !TryScheduleBuild(
                    downstreamDependencyDefinition.CiConfiguration,
                    context.Console,
                    targetBranch,
                    sourceBranch,
                    pullRequestUrl,
                    buildTypeId,
                    out buildUrl ) )
            {
                return false;
            }
        }

        context.Console.WriteSuccess( $"Changes from '{sourceBranch}' missing in '{downstreamBranch}' branch have been merged in branch '{targetBranch}'." );
        context.Console.WriteSuccess( $"Created pull request: {pullRequestUrl}" );
        context.Console.WriteSuccess( buildUrl == null ? "Build is not required." : $"Scheduled build: {buildUrl}" );

        return true;
    }

    private static bool TryMerge(
        BuildContext context,
        BaseBuildSettings settings,
        string sourceBranch,
        string targetBranch,
        string downstreamBranch,
        out bool areChangesPending )
    {
        areChangesPending = false;

        context.Console.WriteImportantMessage( $"Merging '{sourceBranch}' branch to '{targetBranch}' branch" );

        if ( !GitHelper.TryMerge( context, sourceBranch, targetBranch, "--no-commit --no-ff", true ) )
        {
            return false;
        }

        if ( !GitHelper.TryGetStatus( context, context.RepoDirectory, out var statuses )
             
             // We don't rely on the number of changed files, because when there are commits with the same changes,
             // it results in a merge commit with zero changed files.
             || !GitHelper.TryGetIsMergeInProgress( context, context.RepoDirectory, out var isMergeInProgress ) )
        {
            return false;
        }

        if ( isMergeInProgress )
        {
            if ( statuses.Length > 0 )
            {
                context.Console.WriteImportantMessage( "Checking the merged files for those we want to keep own." );
                
                // We don't merge these files downstream as they are specific to the product family version.
                var filesToKeepOwn = new HashSet<string>();

                void AddFileToKeepOwn( string path )
                {
                    var unixStylePath = path.Replace( Path.DirectorySeparatorChar, '/' );
                    filesToKeepOwn.Add( unixStylePath );
                }

                AddFileToKeepOwn( context.Product.MainVersionFilePath );
                AddFileToKeepOwn( context.Product.AutoUpdatedVersionsFilePath );
                AddFileToKeepOwn( context.Product.BumpInfoFilePath );

                Directory.EnumerateFiles( Path.Combine( context.RepoDirectory, ".teamcity" ), "*", SearchOption.AllDirectories )
                    .Select( p => Path.GetRelativePath( context.RepoDirectory, p ) )
                    .ToList()
                    .ForEach( AddFileToKeepOwn );

                foreach ( var status in statuses.Select( s => s.Split( ' ', 2, StringSplitOptions.TrimEntries ) ) )
                {
                    var fileToResolve = status[1];

                    if ( filesToKeepOwn.Contains( fileToResolve ) )
                    {
                        if ( !GitHelper.TryResolveUsingOurs( context, fileToResolve ) )
                        {
                            return false;
                        }
                    }
                }
            }

            // If not all conflicts were expected, git commit fails here.
            if ( !GitHelper.TryCommitMerge( context ) )
            {
                context.Console.WriteError( $"Merge conflicts need to be resolved manually. Merge '{sourceBranch}' branch to '{targetBranch}' branch." );
                context.Console.WriteError( $"Then create a pull request to '{downstreamBranch}' branch or execute this command again." );

                return false;
            }
        }

        // We push even if there's nothing to merge as there could be commits from manual conflict resolution.
        if ( !GitHelper.TryPush( context, settings ) )
        {
            areChangesPending = false;

            return false;
        }

        context.Console.WriteImportantMessage( $"'{sourceBranch}' branch merged to '{targetBranch}' branch." );
        areChangesPending = true;

        return true;
    }

    private static bool TryCreatePullRequest(
        BuildContext context,
        string targetBranch,
        string downstreamBranch,
        string sourceBranch,
        [NotNullWhen( true )] out string? pullRequestUrl )
    {
        context.Console.WriteImportantMessage( $"Creating pull request from '{targetBranch}' branch to '{downstreamBranch}' downstream branch" );

        if ( !GitHelper.TryGetRemoteUrl( context, out var remoteUrl ) )
        {
            pullRequestUrl = null;

            return false;
        }

        try
        {
            var pullRequestTitle = $"Downstream merge from '{sourceBranch}' branch";
            Task<string?> newPullRequestTask;

            if ( AzureDevOpsRepository.TryParse( remoteUrl, out var azureDevOpsRepository ) )
            {
                newPullRequestTask = AzureDevOpsHelper.TryCreatePullRequest(
                    context.Console,
                    azureDevOpsRepository,
                    targetBranch,
                    downstreamBranch,
                    pullRequestTitle );
            }
            else if ( GitHubRepository.TryParse( remoteUrl, out var gitHubRepository ) )
            {
                newPullRequestTask = GitHubHelper.TryCreatePullRequestAsync(
                    context.Console,
                    gitHubRepository,
                    targetBranch,
                    downstreamBranch,
                    pullRequestTitle );
            }
            else
            {
                context.Console.WriteError( $"Unknown VCS or unexpected repo URL format. Repo URL: '{remoteUrl}'." );
                pullRequestUrl = null;

                return false;
            }

            pullRequestUrl = newPullRequestTask.ConfigureAwait( false ).GetAwaiter().GetResult();

            if ( pullRequestUrl == null )
            {
                return false;
            }
        }
        catch ( Exception e )
        {
            context.Console.WriteError( e.ToString() );
            pullRequestUrl = null;

            return false;
        }

        context.Console.WriteImportantMessage( $"Pull request created. {pullRequestUrl}" );

        return true;
    }

    private static bool TryScheduleBuild(
        CiProjectConfiguration ciConfiguration,
        ConsoleHelper console,
        string targetBranch,
        string sourceBranch,
        string pullRequestUrl,
        string buildTypeId,
        [NotNullWhen( true )] out string? buildUrl )
    {
        console.WriteImportantMessage( $"Scheduling build '{buildTypeId}' of '{targetBranch}' branch" );

        if ( !TeamCityHelper.TryConnectTeamCity( ciConfiguration, console, out var tc ) )
        {
            buildUrl = null;

            return false;
        }

        var buildId = tc.ScheduleBuild(
            console,
            buildTypeId,
            $"Triggered by PostSharp.Engineering for downstream merge from '{sourceBranch}' branch to auto-complete pull request {pullRequestUrl}",
            targetBranch );

        if ( buildId == null )
        {
            buildUrl = null;

            return false;
        }

        buildUrl = $"https://postsharp.teamcity.com/viewLog.html?buildId={buildId}";
        console.WriteImportantMessage( $"Build scheduled. {buildUrl}" );

        return true;
    }
}