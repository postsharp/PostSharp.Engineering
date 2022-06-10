namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class VcsProvider
    {
        public bool SshAgentRequired { get; init; }

        public static readonly VcsProvider None = new();
        public static readonly VcsProvider GitHub = new() { SshAgentRequired = true };
        public static readonly VcsProvider AzureRepos = new();
    }
}