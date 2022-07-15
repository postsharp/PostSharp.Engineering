using System;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public abstract class VcsProvider
    {
        public abstract bool SshAgentRequired { get; }

        public abstract string GetRepoUrl( string name, string projectName );

        public static readonly VcsProvider GitHub = new GitHubProvider();
        public static readonly VcsProvider AzureRepos = new AzureRepoProvider();
        
        private class GitHubProvider : VcsProvider
        {
            public override bool SshAgentRequired => true;

            public override string GetRepoUrl( string name, string projectName ) => $"https://github.com/postsharp/{name}.git";
        }
        
        private class AzureRepoProvider : VcsProvider
        {
            public override bool SshAgentRequired => false;

            public override string GetRepoUrl( string name, string projectName ) => $"https://postsharp@dev.azure.com/postsharp/{projectName}/_git/{name}.Vsx";
        }
        
    }
}