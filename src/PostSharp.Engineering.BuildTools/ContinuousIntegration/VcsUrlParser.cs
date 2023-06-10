// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Diagnostics.CodeAnalysis;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public static class VcsUrlParser
{
    public static bool TryGetName( string url, [NotNullWhen( true )] out string? name )
    {
        if ( AzureDevOpsRepoUrlParser.TryParse( url, out _, out _, out name ) )
        {
            return true;
        }
        else if ( GitHubRepoUrlParser.TryParse( url, out _, out name ) )
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}