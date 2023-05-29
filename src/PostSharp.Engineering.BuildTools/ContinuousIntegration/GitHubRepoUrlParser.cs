using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public static class GitHubRepoUrlParser
{
    // E.g.
    // git@github.com:postsharp/Metalama.Documentation.git // Used on TeamCity
    // https://github.com/postsharp/Metalama.Documentation.git // Used locally
    private static readonly Regex _urlRegex = new Regex( "^(?:git@github.com:|https://github.com/)(?<owner>[^/]+)/(?<repo>[^/]+)\\.git$" );

    public static bool TryParse( string repoUrl, [NotNullWhen( true )] out string? repoOwner, [NotNullWhen( true )] out string? repoName )
    {
        var match = _urlRegex.Match( repoUrl );

        if ( !match.Success )
        {
            repoOwner = null;
            repoName = null;

            return false;
        }

        repoOwner = match.Groups["owner"].Value;
        repoName = match.Groups["repo"].Value;

        return true;
    }
}