namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class VcsProvider
    {
        public bool SshAgentRequired { get; init; }

        public static VcsProvider None = new VcsProvider();
        public static VcsProvider GitHub = new VcsProvider() { SshAgentRequired = true };
        public static VcsProvider AzureRepos = new VcsProvider();
    }
}