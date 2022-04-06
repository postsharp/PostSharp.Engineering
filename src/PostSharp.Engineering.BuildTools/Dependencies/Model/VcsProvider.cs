namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class VcsProvider
    {
        public bool SshAgentRequired { get; init; }

        public static readonly VcsProvider None = new VcsProvider();
        public static readonly VcsProvider GitHub = new VcsProvider() { SshAgentRequired = true };
        public static readonly VcsProvider AzureRepos = new VcsProvider();
    }
}