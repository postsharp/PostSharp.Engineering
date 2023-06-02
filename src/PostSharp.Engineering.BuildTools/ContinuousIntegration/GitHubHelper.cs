// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Octokit;
using Octokit.GraphQL;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using PullRequestMergeMethod = Octokit.GraphQL.Model.PullRequestMergeMethod;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public static class GitHubHelper
{
    private const string _productHeaderName = "PostSharp.Engineering";
    private static readonly string _productHeaderVersion = typeof(GitHubHelper).Assembly.GetName().Version!.ToString(); 
    
    private static bool TryGetToken( ConsoleHelper console, [NotNullWhen( true )] out string? token )
    {
        const string tokenEnvironmentVariableName = "GITHUB_TOKEN";

        token = Environment.GetEnvironmentVariable( tokenEnvironmentVariableName );

        if ( string.IsNullOrEmpty( token ) )
        {
            console.WriteError( $"The '{tokenEnvironmentVariableName}' environment variable is not defined." );
            token = null;

            return false;
        }

        return true;
    }

    private static bool TryConnectRestApi( ConsoleHelper console, [NotNullWhen( true )] out GitHubClient? client )
    {
        if ( !TryGetToken( console, out var token ) )
        {
            client = null;

            return false;
        }

        client = new( new Octokit.ProductHeaderValue( _productHeaderName, _productHeaderVersion ) ) { Credentials = new Credentials( token ) };

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

    public static async Task<string?> TryCreatePullRequest(
        ConsoleHelper console,
        string repoOwner,
        string repoName,
        string sourceBranch,
        string targetBranch,
        string title )
    {
        if ( !TryConnectRestApi( console, out var gitHub ) )
        {
            return null;
        }

        if ( !TryConnectGraphQl( console, out var graphQl ) )
        {
            return null;
        }

        console.WriteMessage( "Creating pull request." );
        var newPullRequest = new NewPullRequest( title, sourceBranch, targetBranch );
        var pullRequest = await gitHub.PullRequest.Create( repoOwner, repoName, newPullRequest );

        var pullRequestQuery = new Query()
            .RepositoryOwner( repoOwner )
            .Repository( repoName )
            .Select( repo => repo.PullRequest( pullRequest.Number ) )
            .Select( pr => pr.Id )
            .Compile();

        console.WriteMessage( "Enabling pull request auto-merge." );
        var pullRequestId = await graphQl.Run( pullRequestQuery );

        var authorEmail = Environment.GetEnvironmentVariable( "GITHUB_AUTHOR_EMAIL" ) ?? "teamcity@postsharp.net";
        
        var enableAutoMergeMutation = new Mutation()
            .EnablePullRequestAutoMerge(
                new(
                    new()
                    {
                        AuthorEmail = authorEmail,
                        CommitHeadline = title,
                        MergeMethod = PullRequestMergeMethod.Merge,
                        PullRequestId = pullRequestId
                    } ) )
            .Select( am => am.ClientMutationId ) // We need to select something to avoid ResponseDeserializerException
            .Compile();

        console.WriteMessage( "Setting the new pull request to get completed automatically." );
        _ = await graphQl.Run( enableAutoMergeMutation );

        var url = $"https://github.com/{repoOwner}/{repoName}/pull/{pullRequest.Number}";

        return url;
    }
}