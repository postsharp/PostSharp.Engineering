// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public static class AzureDevOpsHelper
{
    private static bool TryConnect( ConsoleHelper console, string url, [NotNullWhen( true )] out VssConnection? connection )
    {
        var user = Environment.GetEnvironmentVariable( "AZURE_DEVOPS_USER" ) ?? "teamcity@postsharp.net";

        const string tokenEnvironmentVariableName = "AZURE_DEVOPS_TOKEN";

        var token = Environment.GetEnvironmentVariable( tokenEnvironmentVariableName );

        if ( string.IsNullOrEmpty( token ) )
        {
            console.WriteError( $"The '{tokenEnvironmentVariableName}' environment variable is not defined." );
            connection = null;

            return false;
        }

        var credentials = new VssBasicCredential( user, token );
        connection = new VssConnection( new Uri( url ), credentials );

        return true;
    }

    // https://github.com/microsoft/azure-devops-dotnet-samples/blob/main/ClientLibrary/Samples/Git/PullRequestsSample.cs
    // https://stackoverflow.com/a/52025418/4100001
    // Required personal access token scopes: Code: Read&Write
    public static async Task<string?> TryCreatePullRequest(
        ConsoleHelper console,
        string baseUrl,
        string projectName,
        string repoName,
        string sourceBranch,
        string targetBranch,
        string title )
    {
        try
        {
            if ( !TryConnect( console, baseUrl, out var azureDevOps ) )
            {
                return null;
            }

            using ( azureDevOps )
            {
                using var azureDevOpsGit = azureDevOps.GetClient<GitHttpClient>();

                var pullRequest = new GitPullRequest()
                {
                    SourceRefName = $"refs/heads/{sourceBranch}", TargetRefName = $"refs/heads/{targetBranch}", Title = title
                };

                console.WriteMessage( "Creating a pull request." );
                var createdPullRequest = await azureDevOpsGit.CreatePullRequestAsync( pullRequest, projectName, repoName );

                console.WriteMessage( "Approving the pull request." );

                _ = await azureDevOpsGit.CreatePullRequestReviewerAsync(
                    new IdentityRefWithVote( new IdentityRef { Id = createdPullRequest.CreatedBy.Id } ) { Vote = 10 },
                    projectName,
                    repoName,
                    createdPullRequest.PullRequestId,
                    createdPullRequest.CreatedBy.Id );

                var pullRequestWithAutoCompleteEnabled = new GitPullRequest
                {
                    AutoCompleteSetBy = new IdentityRef { Id = createdPullRequest.CreatedBy.Id },
                    CompletionOptions = new GitPullRequestCompletionOptions { DeleteSourceBranch = true, MergeCommitMessage = title },
                };

                console.WriteMessage( "Setting the new pull request to get completed automatically." );

                var updatedPullRequest = await azureDevOpsGit.UpdatePullRequestAsync(
                    pullRequestWithAutoCompleteEnabled,
                    projectName,
                    repoName,
                    createdPullRequest.PullRequestId );

                var url = $"{baseUrl}/{projectName}/_git/{repoName}/pullrequest/{createdPullRequest.PullRequestId}";
                
                console.WriteSuccess( $"Created pull request: {url}" );
                
                return url;
            }
        }
        catch ( Exception e )
        {
            console.WriteError( e.ToString() );

            return null;
        }
    }
}