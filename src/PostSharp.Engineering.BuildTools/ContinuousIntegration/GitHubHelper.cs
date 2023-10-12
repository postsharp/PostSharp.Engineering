// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Environment = System.Environment;
using PullRequestMergeMethod = Octokit.GraphQL.Model.PullRequestMergeMethod;
using PullRequestReviewEvent = Octokit.PullRequestReviewEvent;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public static class GitHubHelper
{
    private const string _tokenEnvironmentVariableName = "GITHUB_TOKEN";
    private const string _reviewerTokenEnvironmentVariableName = "GITHUB_REVIEWER_TOKEN";
    private const string _productHeaderName = "PostSharp.Engineering";
    private static readonly string _productHeaderVersion = typeof(GitHubHelper).Assembly.GetName().Version!.ToString();

    private static bool TryGetToken(
        ConsoleHelper console,
        [NotNullWhen( true )] out string? token,
        string tokenEnvironmentVariableName = _tokenEnvironmentVariableName )
    {
        token = Environment.GetEnvironmentVariable( tokenEnvironmentVariableName );

        if ( string.IsNullOrEmpty( token ) )
        {
            console.WriteError( $"The '{tokenEnvironmentVariableName}' environment variable is not defined." );
            token = null;

            return false;
        }

        return true;
    }

    private static GitHubClient ConnectRestApi( string token )
        => new( new Octokit.ProductHeaderValue( _productHeaderName, _productHeaderVersion ) ) { Credentials = new Credentials( token ) };

    private static bool TryConnectRestApi(
        ConsoleHelper console,
        [NotNullWhen( true )] out GitHubClient? client,
        string tokenEnvironmentVariableName = _tokenEnvironmentVariableName )
    {
        if ( !TryGetToken( console, out var token, tokenEnvironmentVariableName ) )
        {
            client = null;

            return false;
        }

        client = ConnectRestApi( token );

        return true;
    }

    private static bool TryConnectGraphQl( ConsoleHelper console, [NotNullWhen( true )] out Octokit.GraphQL.Connection? connection )
    {
        if ( !TryGetToken( console, out var token ) )
        {
            connection = null;

            return false;
        }

        connection = new( new Octokit.GraphQL.ProductHeaderValue( _productHeaderName, _productHeaderVersion ), token );

        return true;
    }

    public static async Task<string?> TryCreatePullRequestAsync(
        ConsoleHelper console,
        GitHubRepository repository,
        string sourceBranch,
        string targetBranch,
        string title )
    {
        bool TryConnectRestApis( [NotNullWhen( true )] out GitHubClient? c, [NotNullWhen( true )] out GitHubClient? r )
        {
            c = null;
            r = null;

            if ( !TryGetToken( console, out var creatorToken ) )
            {
                return false;
            }

            if ( !TryGetToken( console, out var reviewerToken, _reviewerTokenEnvironmentVariableName ) )
            {
                return false;
            }

            c = ConnectRestApi( creatorToken );
            r = creatorToken == reviewerToken ? c : ConnectRestApi( reviewerToken );

            return true;
        }

        if ( !TryConnectRestApis( out var creatorGitHub, out var reviewerGitHub ) )
        {
            return null;
        }

        if ( !TryConnectGraphQl( console, out var graphQl ) )
        {
            return null;
        }

        console.WriteMessage( "Creating pull request." );
        var newPullRequest = new NewPullRequest( title, sourceBranch, targetBranch );
        var pullRequest = await creatorGitHub.PullRequest.Create( repository.Owner, repository.Name, newPullRequest );
        console.WriteMessage( $"Pull request created: {pullRequest.Url}" );

        // A pull request cannot be self-reviewed on GitHub.
        // https://github.com/orgs/community/discussions/6292
        var reviewerLogin = reviewerGitHub.User.Current().Result.Login;
        console.WriteMessage( $"Requesting a review of the pull request from '{reviewerLogin}' user." );
        var reviewRequest = new PullRequestReviewRequest( new List<string> { reviewerLogin }, new List<string>() );
        pullRequest = await reviewerGitHub.PullRequest.ReviewRequest.Create( repository.Owner, repository.Name, pullRequest.Number, reviewRequest );

        console.WriteMessage( "Approving the pull request." );
        var pullRequestApproval = new PullRequestReviewCreate { Event = PullRequestReviewEvent.Approve };
        _ = await reviewerGitHub.PullRequest.Review.Create( repository.Owner, repository.Name, pullRequest.Number, pullRequestApproval );

        console.WriteMessage( "Enabling pull request auto-merge." );

        var pullRequestQuery = new Query()
            .RepositoryOwner( repository.Owner )
            .Repository( repository.Name )
            .Select( repo => repo.PullRequest( pullRequest.Number ) )
            .Select( pr => pr.Id )
            .Compile();

        var pullRequestId = await graphQl.Run( pullRequestQuery );

        var authorEmail = Environment.GetEnvironmentVariable( "GITHUB_AUTHOR_EMAIL" ) ?? "teamcity@postsharp.net";

        var enableAutoMergeMutation = new Mutation()
            .EnablePullRequestAutoMerge(
                new( new() { AuthorEmail = authorEmail, CommitHeadline = title, MergeMethod = PullRequestMergeMethod.Merge, PullRequestId = pullRequestId } ) )
            .Select( am => am.ClientMutationId ) // We need to select something to avoid ResponseDeserializerException
            .Compile();

        _ = await graphQl.Run( enableAutoMergeMutation );

        var url = $"https://github.com/{repository.Owner}/{repository.Name}/pull/{pullRequest.Number}";

        return url;
    }

    public static async Task<bool> TrySetBranchPoliciesAsync(
        BuildContext context,
        GitHubRepository gitHubRepository,
        string buildStatusGenre,
        string? buildStatusName,
        bool dry )
    {
        if ( !TryConnectGraphQl( context.Console, out var graphQl ) )
        {
            return false;
        }

        var branch = context.Product.DependencyDefinition.Branch;

        context.Console.WriteMessage( $"Setting protection rule for '{branch}' branch." );

        var repositoryIdQuery = new Query()
            .RepositoryOwner( gitHubRepository.Owner )
            .Repository( gitHubRepository.Name )
            .Select( r => r.Id )
            .Compile();

        var repositoryId = await graphQl.Run( repositoryIdQuery );

        var ruleMutation = new Mutation()
            .CreateBranchProtectionRule(
                new(
                    new()
                    {
                        RepositoryId = repositoryId,
                        Pattern = branch,
                        RequiresApprovingReviews = true,
                        RequiredApprovingReviewCount = 1,
                        RequiresStatusChecks = buildStatusName != null,
                        RequiredStatusChecks =
                            buildStatusName == null
                                ? Array.Empty<RequiredStatusCheckInput>()
                                : new[] { new RequiredStatusCheckInput { Context = $"{buildStatusGenre}/{buildStatusName}" } },
                        RequiresConversationResolution = true
                    } ) )
            .Select( r => r.BranchProtectionRule ) // We need to select something to avoid ResponseDeserializerException
            .Select( r => r.Pattern )
            .Compile();

        if ( !dry )
        {
            _ = await graphQl.Run( ruleMutation );
        }

        branch = context.Product.DependencyDefinition.ReleaseBranch;

        if ( branch != null )
        {
            context.Console.WriteMessage( $"Setting protection rule for '{branch}' branch." );

            ruleMutation = new Mutation()
                .CreateBranchProtectionRule(
                    new(
                        new()
                        {
                            RepositoryId = repositoryId,
                            Pattern = branch,
                            RequiresApprovingReviews = true,
                            RequiredApprovingReviewCount = 1,
                            RequiresConversationResolution = true
                        } ) )
                .Select( r => r.BranchProtectionRule ) // We need to select something to avoid ResponseDeserializerException
                .Select( r => r.Pattern )
                .Compile();

            if ( !dry )
            {
                _ = await graphQl.Run( ruleMutation );
            }
        }

        return true;
    }
    
    public static async Task<bool> TrySetDefaultBranchAsync(
        ConsoleHelper console,
        GitHubRepository gitHubRepository,
        string defaultBranch,
        bool dry )
    {
        console.WriteMessage( $"Setting repository default branch to '{defaultBranch}'." );
        
        if ( !TryConnectRestApi( console, out var gitHub ) )
        {
            return false;
        }

        var repositoryUpdate = new RepositoryUpdate() { DefaultBranch = defaultBranch };
        
        if ( !dry )
        {
            _ = await gitHub.Repository.Edit( gitHubRepository.Owner, gitHubRepository.Name, repositoryUpdate );
        }

        return true;
    }
}