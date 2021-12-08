using PostSharp.Engineering.BuildTools.Build.Model;
using System;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class DependencyDefinition
    {
        public string Name { get; }

        public string RepoName { get; init; }

        public string DefaultBranch { get; init; } = "master";

        public string VcsProjectName { get; }

        public string? CiBuildTypeId { get; }

        public VcsProvider Provider { get; }

        public DependencyDefinition( string name, VcsProvider provider, string vcsProjectName, string? ciBuildTypeId = null )
        {
            this.Name = name;
            this.Provider = provider;
            this.VcsProjectName = vcsProjectName;
            this.RepoName = name;
            this.CiBuildTypeId = ciBuildTypeId;
        }

        public string GetLocalDirectory( int buildNumber )
            => Path.Combine(
                Environment.GetEnvironmentVariable( "USERPROFILE" ) ?? Path.GetTempPath(),
                ".build-artifacts",
                this.RepoName,
                this.CiBuildTypeId!,
                buildNumber.ToString() );
    }
}