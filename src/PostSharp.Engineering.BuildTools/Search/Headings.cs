// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Search;

internal class Headings
{
    private readonly string?[] _titles = new string?[9];

    public bool AreEmpty => this._titles.All( t => t == null );

    public int Level
    {
        get
        {
            int i;

            for ( i = this._titles.Length - 1; i >= 0; i-- )
            {
                if ( this._titles[i] != null )
                {
                    break;
                }
            }

            return i + 1;
        }
    }

    public int Rank => 100 - this.Level - 1;

    public void Set( int level, string title ) => this.SetImpl( level, title );

    public void Reset( int level ) => this.SetImpl( level, null );

    private void SetImpl( int level, string? title )
    {
        if ( level < 1 || level > 9 )
        {
            throw new ArgumentOutOfRangeException( nameof(level), level, "Level must be 1-9." );
        }

        this._titles[level - 1] = title;

        for ( var i = level; i < this._titles.Length; i++ )
        {
            this._titles[i] = null;
        }
    }

    public override string ToString() => string.Join( " > ", this._titles.Where( t => t != null ) );
}