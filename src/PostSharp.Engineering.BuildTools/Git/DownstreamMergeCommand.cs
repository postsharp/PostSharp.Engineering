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
                $"Downstream merge can only be executed on the default branch ('{sourceBranch}'). The current branch is '{context.Branch}'." );

            return false;
        }

        context.Console.WriteHeading( $"Executing downstream merge from '{sourceBranch}' branch to '{downstreamBranch}' branch" );

        if ( !GitHelper.TryGetCurrentCommitHash( context, out var sourceCommitHash ) )
        {
            return false;
        }

        context.Console.WriteImportantMessage( $"Pulling changes from '{downstreamBranch}' downstream branch" );

        if ( !GitHelper.TryCheckoutAndPull( context, downstreamBranch, out _ ) )
        {
            return false;
        }

        if ( !GitHelper.TryGetCommitsCount( context, "HEAD", sourceBranch, out var commitsCount ) )
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

        context.Console.WriteMessage( $"There are {commitsCount} commits to merge from '{sourceBranch}' branch to '{downstreamBranch}' branch." );

        context.Console.WriteImportantMessage( $"Downstream changes from '{downstreamBranch}' downstream branch were pulled." );

        var targetBranch = $"merge/{downstreamProductFamily.Version}/{context.Product.ProductFamily.Version}-{sourceCommitHash}";

        context.Console.WriteImportantMessage( $"Creating '{targetBranch}' target branch" );

        // If the targetBranch exists already, use it. Otherwise, create it.
        if ( GitHelper.TryCheckoutAndPull( context, targetBranch, out var remoteNotFound ) && !remoteNotFound )
        {
            return false;
        }
        else if ( !remoteNotFound )
        {
            context.Console.WriteImportantMessage( $"The '{targetBranch}' target branch already exists. Let's use it." );
        }
        else
        {
            context.Console.WriteMessage( $"The '{targetBranch}' target branch doesn't exits. Let's create it." );

            if ( !GitHelper.TryCreateBranch( context, targetBranch ) )
            {
                return false;
            }

            // Push the new branch now to avoid issues when the DownstreamMergeCommand
            // is executed again with the same upstream changes.
            if ( !GitHelper.TryPush( context, settings ) )
            {
                return false;
            }

            context.Console.WriteImportantMessage( $"The '{targetBranch}' target was created." );
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

        var buildTypeId = $"{downstreamDependencyDefinition.CiConfiguration.ProjectId}_DebugBuild";

        if ( !TryScheduleBuild(
                downstreamDependencyDefinition.CiConfiguration,
                context.Console,
                targetBranch,
                sourceBranch,
                pullRequestUrl,
                buildTypeId,
                out var buildUrl ) )
        {
            return false;
        }

        context.Console.WriteSuccess( $"Changes from '{sourceBranch}' missing in '{downstreamBranch}' branch have been merged in branch '{targetBranch}'." );
        context.Console.WriteSuccess( $"Created pull request: {pullRequestUrl}" );
        context.Console.WriteSuccess( $"Scheduled build: {buildUrl}" );

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

        context.Console.WriteImportantMessage( "Checking the merged files for those we want to keep own." );
        
        if ( !GitHelper.TryGetStatus( context, context.RepoDirectory, out var statuses ) )
        {
            return false;
        }

        if ( statuses.Length > 0 )
        {
            // We don't merge these files downstream as they are specific to the product family version.
            var filesToKeepOwn = new HashSet<string>();

            void AddFileToKeepOwn( string path )
            {
                var unixStylePath = path.Replace( Path.DirectorySeparatorChar, '/' );
                filesToKeepOwn.Add( unixStylePath );
            }

            AddFileToKeepOwn( context.Product.MainVersionFilePath );
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
            
            // If not all conflicts were expected, git commit fails here.
            if ( !GitHelper.TryCommitMerge( context ) )
            {
                context.Console.WriteError(
                    $"Merge conflicts need to be resolved manually. Merge '{sourceBranch}' branch to '{targetBranch}' branch. Then create a pull request to '{downstreamBranch}' branch or execute this command again." );

                return false;
            }
        }
        else
        {
            // If there is nothing to merge at this point, we check if the target branch contains some commits
            // from previous run of the command or from another source.
            // (Eg. developers handling downstream merge conflicts manually.)
            
            if ( !GitHelper.TryGetCommitsCount( context, downstreamBranch, "HEAD", out var commitsCount ) )
            {
                return false;
            }

            if ( commitsCount < 0 )
            {
                throw new InvalidOperationException( $"Invalid commits count: {commitsCount}" );
            }

            if ( commitsCount == 0 )
            {
                return true;
            }
        }

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