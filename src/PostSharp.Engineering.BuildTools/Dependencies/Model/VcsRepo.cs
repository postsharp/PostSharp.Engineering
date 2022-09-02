// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public class VcsRepo
{
    public string RepoName { get; }

    public string ProjectName { get; }

    public VcsProvider Provider { get; }

    public string RepoUrl => this.Provider.GetRepoUrl( this );

    public string DownloadTextFile( string branch, string path ) => this.Provider.DownloadTextFile( this, branch, path );

    public VcsRepo( string repoName, string projectName, VcsProvider provider )
    {
        this.RepoName = repoName;
        this.ProjectName = projectName;
        this.Provider = provider;
    }
}