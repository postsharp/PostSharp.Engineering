// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PostSharp.Engineering.BuildTools.Utilities;

public class GitIgnore
{
    private readonly List<Regex> _regexPatterns;

    public GitIgnore( string gitIgnorePath )
    {
        this._regexPatterns =
            File.ReadAllLines( gitIgnorePath )
                .Where( line => !string.IsNullOrWhiteSpace( line ) && !line.TrimStart().StartsWith( "#", StringComparison.Ordinal ) )
                .Select( pattern => new Regex( ConvertGitIgnorePatternToRegex( pattern ), RegexOptions.Compiled | RegexOptions.IgnoreCase ) )
                .ToList();
    }

    public bool ShouldIgnore( string filePath )
    {
        var relativePath = filePath.Replace( '\\', '/' );

        return this._regexPatterns.Any( pattern => pattern.IsMatch( relativePath ) );
    }

    private static string ConvertGitIgnorePatternToRegex( string pattern )
    {
        pattern = pattern.Trim();

        // Handle negation
        var isNegated = pattern.StartsWith( "!" );

        if ( isNegated )
        {
            pattern = pattern.Substring( 1 );
        }

        // Escape regex special characters
        pattern = Regex.Escape( pattern );

        // Convert ** to .*
        pattern = pattern.Replace( @"\*\*", ".*", StringComparison.Ordinal );

        // Convert * to [^/]*
        pattern = pattern.Replace( @"\*", "[^/]*", StringComparison.Ordinal );

        // Convert ? to .
        pattern = pattern.Replace( @"\?", ".", StringComparison.Ordinal );

        // Handle trailing slash for directories
        if ( pattern.EndsWith( "/", StringComparison.Ordinal ) )
        {
            pattern = pattern.TrimEnd( '/' );
            pattern += "(/.*)?";
        }
        else
        {
            pattern += "(/.*)?";
        }

        // Handle starting slash
        if ( pattern.StartsWith( "/", StringComparison.Ordinal ) )
        {
            pattern = "^" + pattern.Substring( 1 );
        }
        else
        {
            pattern = "^(.*?/)?(" + pattern + ")";
        }

        if ( isNegated )
        {
            pattern = "^((?!" + pattern + ").)*$";
        }

        return pattern;
    }
}