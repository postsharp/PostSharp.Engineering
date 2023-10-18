// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public class GitHubRepository : VcsRepository
{
    // E.g.
    // git@github.com:postsharp/Metalama.Documentation.git // Used on TeamCity
    // https://github.com/postsharp/Metalama.Documentation.git // Used locally
    private static readonly Regex _urlRegex = new Regex( "^(?:git@github.com:|https://github.com/)(?<owner>[^/]+)/(?<repo>[^/]+)\\.git$" );
    
    public override string Name { get; }

    public override VcsProvider Provider => VcsProvider.GitHub;

    public string Owner { get; }

    public override string SshUrl => $"git@github.com:{this.Owner}/{this.Name}.git";

    public override string HttpUrl => $"https://github.com/{this.Owner}/{this.Name}.git";
    
    public override string DeveloperMachineRemoteUrl => this.HttpUrl;

    public override string TeamCityRemoteUrl => this.SshUrl;
    
    public override bool IsSshAgentRequired => true;

    public GitHubRepository( string name, string owner = "postsharp" )
    {
        this.Name = name;
        this.Owner = owner;
    }
    
    public static bool TryParse( string repoUrl, [NotNullWhen( true )] out GitHubRepository? repository )
    {
        var match = _urlRegex.Match( repoUrl );

        if ( !match.Success )
        {
            repository = null;
            
            return false;
        }
        
        var owner = match.Groups["owner"].Value;
        var name = match.Groups["repo"].Value;
        repository = new GitHubRepository( name, owner );

        return true;
    }

    public override bool TryDownloadTextFile( ConsoleHelper console, string branch, string path, [NotNullWhen( true )] out string? text )
        => GitHubHelper.TryDownloadText( console, this, path, branch, out text );
}