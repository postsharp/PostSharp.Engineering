using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class Pattern
    {
        private ImmutableArray<(ParametricString Pattern, bool IsExclude)> Items { get; }

        private Pattern( ImmutableArray<(ParametricString Pattern, bool IsExclude)> items )
        {
            this.Items = items;
        }

        public static Pattern Empty { get; } = new( ImmutableArray<(ParametricString Pattern, bool IsExclude)>.Empty );

        public Pattern Add( params ParametricString[] patterns ) => new( this.Items.AddRange( patterns.Select( p => (p, false) ) ) );

        public Pattern Add( Pattern pattern ) => new( this.Items.AddRange( pattern.Items ) );

        public Pattern Remove( params ParametricString[] patterns ) => new( this.Items.AddRange( patterns.Select( p => (p, true) ) ) );

        public static Pattern Create( params ParametricString[] patterns ) => Pattern.Empty.Add( patterns );

        internal bool TryGetFiles( string directory, VersionInfo versionInfo, List<FilePatternMatch> files )
        {
            var matcher = new Matcher( StringComparison.OrdinalIgnoreCase );

            foreach ( var pattern in this.Items )
            {
                var file = pattern.Pattern.ToString( versionInfo );

                if ( pattern.IsExclude )
                {
                    matcher.AddExclude( file );
                }
                else
                {
                    matcher.AddInclude( file );
                }
            }

            var matchingResult =
                matcher.Execute( new DirectoryInfoWrapper( new DirectoryInfo( directory ) ) );

            if ( !matchingResult.HasMatches )
            {
                return false;
            }
            else
            {
                files.AddRange( matchingResult.Files );
            }

            return true;
        }

        public override string ToString() => string.Join( " ", this.Items.Select( i => (i.IsExclude ? "-" : "+") + i.Pattern ) );
    }
}