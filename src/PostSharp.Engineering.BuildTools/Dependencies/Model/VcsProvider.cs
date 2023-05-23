// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public abstract class VcsProvider
    {
        public abstract bool SshAgentRequired { get; }

        public abstract string GetRepoUrl( VcsRepo repo );

        public abstract string DownloadTextFile( VcsRepo repo, string branch, string path );

        public static readonly VcsProvider GitHub = new GitHubProvider();
        public static readonly VcsProvider AzureRepos = new AzureRepoProvider();

        public abstract string DefaultBranch { get; }
        
        public abstract string? DefaultPublishingBranch { get; }

        private class GitHubProvider : VcsProvider
        {
            public override bool SshAgentRequired => true;

            public override string GetRepoUrl( VcsRepo repo ) => $"https://github.com/postsharp/{repo.RepoName}.git";

            public override string DownloadTextFile( VcsRepo repo, string branch, string path )
            {
                var httpClient = new HttpClient();

                return httpClient.GetStringAsync( $"https://raw.githubusercontent.com/postsharp/{repo.RepoName}/{branch}/{path}" ).Result;
            }

            public override string DefaultBranch => $"release/{MainVersion.Value}";

            // Source code publishing disabled for 2023.1.
            public override string? DefaultPublishingBranch => null;
        }

        private class AzureRepoProvider : VcsProvider
        {
            public override bool SshAgentRequired => false;

            public override string GetRepoUrl( VcsRepo repo ) => $"https://postsharp@dev.azure.com/postsharp/{repo.ProjectName}/_git/{repo.RepoName}";

            public override string DownloadTextFile( VcsRepo repo, string branch, string path )
            {
                var httpClient = new HttpClient();

                var teamCitySourceReadToken = TeamCityHelper.GetTeamCitySourceReadToken();

                var authString = Convert.ToBase64String( Encoding.UTF8.GetBytes( $@"{TeamCityHelper.TeamCityUsername}:{teamCitySourceReadToken}" ) );
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Basic", authString );

                return httpClient.GetStringAsync(
                        $"https://dev.azure.com/postsharp/{repo.ProjectName}/_apis/git/repositories/{repo.RepoName}/items?path={path}&versionDescriptor.version={branch}" )
                    .Result;
            }

            public override string DefaultBranch => $"release/{MainVersion.Value}";
            
            public override string? DefaultPublishingBranch => null;
        }
    }
}