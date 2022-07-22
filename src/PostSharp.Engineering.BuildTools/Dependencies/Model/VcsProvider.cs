using System.Net.Http;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public abstract class VcsProvider
    {
        public abstract bool SshAgentRequired { get; }

        public abstract string GetRepoUrl( VcsRepo repo );

        public abstract string DownloadTextFile( VcsRepo repo, string branch, string path );

        public static readonly VcsProvider GitHub = new GitHubProvider();
        public static readonly VcsProvider AzureRepos = new AzureRepoProvider();
        
        private class GitHubProvider : VcsProvider
        {
            public override bool SshAgentRequired => true;

            public override string GetRepoUrl( VcsRepo repo ) => $"https://github.com/postsharp/{repo.RepoName}.git";

            public override string DownloadTextFile( VcsRepo repo, string branch, string path )
            {
                var httpClient = new HttpClient();
                return httpClient.GetStringAsync( $"https://raw.githubusercontent.com/postsharp/{repo.RepoName}/{branch}/{path}" ).Result;
            }
        }
        
        private class AzureRepoProvider : VcsProvider
        {
            public override bool SshAgentRequired => false;

            public override string GetRepoUrl( VcsRepo repo ) => $"https://postsharp@dev.azure.com/postsharp/{repo.ProjectName}/_git/{repo.RepoName}";

            public override string DownloadTextFile( VcsRepo repo, string branch, string path ) => throw new System.NotImplementedException();
        }
    }
}