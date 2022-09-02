// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using PostSharp.Engineering.BuildTools.Build.Model;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Build.Solutions;

public class ManyDotNetSolutions : Solution
{
    private ImmutableArray<DotNetSolution> _solutions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManyDotNetSolutions"/> class.
    /// </summary>
    /// <param name="solutions">A clobbing pattern relative to the repo root directory.</param>
    public ManyDotNetSolutions( string solutions ) : base( solutions ) { }

    public override bool Build( BuildContext context, BuildSettings settings )
    {
        var success = true;

        if ( !this.TryGetSolutions( context, out var solutions ) )
        {
            return false;
        }

        foreach ( var solution in solutions )
        {
            success &= solution.Build( context, settings );
        }

        return success;
    }

    public override bool Pack( BuildContext context, BuildSettings settings )
    {
        var success = true;

        if ( !this.TryGetSolutions( context, out var solutions ) )
        {
            return false;
        }

        foreach ( var solution in solutions )
        {
            success &= solution.Pack( context, settings );
        }

        return success;
    }

    public override bool Test( BuildContext context, BuildSettings settings )
    {
        var success = true;

        if ( !this.TryGetSolutions( context, out var solutions ) )
        {
            return false;
        }

        foreach ( var solution in solutions )
        {
            success &= solution.Test( context, settings );
        }

        return success;
    }

    public override bool Restore( BuildContext context, BuildSettings settings )
    {
        var success = true;

        if ( !this.TryGetSolutions( context, out var solutions ) )
        {
            return false;
        }

        foreach ( var solution in solutions )
        {
            success &= solution.Restore( context, settings );
        }

        return success;
    }

    private bool TryGetSolutions( BuildContext context, out ImmutableArray<DotNetSolution> solutions )
    {
        if ( this._solutions.IsDefault )
        {
            var matcher = new Matcher( StringComparison.OrdinalIgnoreCase );
            var pattern = this.SolutionPath;
            matcher.AddInclude( pattern );

            var directory = Path.Combine( context.RepoDirectory );
            var matches = matcher.Execute( new DirectoryInfoWrapper( new DirectoryInfo( directory ) ) );

            if ( !matches.Files.Any() )
            {
                context.Console.WriteError( $"'{directory}\\{pattern}' did not match any file." );

                solutions = default;

                return false;
            }

            this._solutions = matches.Files.Select( x => new DotNetSolution( Path.Combine( directory, x.Path ) ) ).ToImmutableArray();
        }

        solutions = this._solutions;

        return true;
    }
}