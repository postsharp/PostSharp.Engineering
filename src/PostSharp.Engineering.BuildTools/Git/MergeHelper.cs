// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Git;

public class MergeHelper
{
    public static void ExplainUnmergedBranches( ConsoleHelper console, IEnumerable<string> references, bool force, string? filteredBranchesDescription = null )
    {
        void Write( string message )
        {
            if ( force )
            {
                console.WriteWarning( message );
            }
            else
            {
                console.WriteError( message );
            }
        }
        
        Write( "There are former merge branches in the repository." );
        Write( "Before retrying, make sure that there are no important changes in these branches that need to be merged." );
        Write( "Such changes may be created when solving a merge conflict." );
        Write( "You can either finish merging of those branches or delete them." );
        Write( "If a pull request doesn't exist for these branches already, create one manually." );

        if ( force )
        {
            console.WriteWarning( "Existence of these branches is ignored because --force has been used." );
        }

        Write( "" );

        if ( filteredBranchesDescription != null )
        {
            Write( filteredBranchesDescription );
            Write( "" );
        }
            
        Write( "The branches are:" );

        references.Select( r => r.Replace( "refs/heads", "", StringComparison.Ordinal ) )
            .ToList()
            .ForEach( Write );
    }
}