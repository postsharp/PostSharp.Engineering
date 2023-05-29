using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public static class AzureDevOpsRepoUrlParser
{
    // E.g.
    // https://postsharp@dev.azure.com/postsharp/Caravela/_git/Caravela.Repo
    // https://dev.azure.com/postsharp/Caravela/_git/Caravela.Repo
    private static readonly Regex _urlRegex = new Regex( @"^(?<protocol>https)://(?:(?<user>[^/@]+)@)?(?<domain>[^/]+)/(?<organization>[^/]+)?/(?<project>[^/]+)/_git/(?<repo>[^/]+)$" );

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

        baseUrl = $"{match.Groups["protocol"].Value}://{match.Groups["domain"].Value}/{match.Groups["organization"].Value}";
        projectName = match.Groups["project"].Value;
        repoName = match.Groups["repo"].Value;

        return true;
    }
}