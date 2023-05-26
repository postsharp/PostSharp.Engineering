using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public static class AzureDevOpsRepoUrlParser
{
    // E.g. https://postsharp@dev.azure.com/postsharp/Caravela/_git/Caravela
    private static readonly Regex _urlRegex = new Regex( "^(?<url>https://[^/]+/[^/]+)/(?<project>[^/]+)/_git/(?<repo>[^/]+)$" );

    public static bool TryParse(
        string repoUrl,
        [NotNullWhen( true )] out string? baseUrl,
        [NotNullWhen( true )] out string? projectName,
        [NotNullWhen( true )] out string? repoName )
    {
        var match = _urlRegex.Match( repoUrl );

        if ( !match.Success )
        {
            baseUrl = null;
            projectName = null;
            repoName = null;

            return false;
        }

        baseUrl = match.Groups["url"].Value;
        projectName = match.Groups["project"].Value;
        repoName = match.Groups["repo"].Value;

        return true;
    }
}