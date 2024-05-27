// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Collections.Generic;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Utilities;

public static class FileSystemHelper
{
    public static string GetFinalPath( string path )
    {
        var linkTarget = File.ResolveLinkTarget( path, returnFinalTarget: true );

        return linkTarget?.FullName ?? Path.GetFullPath( path );
    }

    public static void CopyFilesRecursively( ConsoleHelper console, string sourcePath, string targetPath, Predicate<string>? predicate = null )
    {
        HashSet<string> expectedTargetFiles = new();

        Directory.CreateDirectory( targetPath );

        // Now Create all of the directories
        foreach ( var dirPath in Directory.GetDirectories( sourcePath, "*", SearchOption.AllDirectories ) )
        {
            Directory.CreateDirectory( dirPath.Replace( sourcePath, targetPath, StringComparison.Ordinal ) );
        }

        // Copy all the files & Replaces any files with the same name
        foreach ( var sourceFile in Directory.GetFiles( sourcePath, "*", SearchOption.AllDirectories ) )
        {
            var targetFile = sourceFile.Replace( sourcePath, targetPath, StringComparison.Ordinal );

            if ( predicate == null || predicate( Path.GetRelativePath( sourcePath, sourceFile ) ) )
            {
                expectedTargetFiles.Add( targetFile );

                if ( !File.Exists( sourceFile ) || File.GetLastWriteTime( sourceFile ) > File.GetLastWriteTime( targetFile ) )
                {
                    console.WriteMessage( $"Copying '{targetFile}'." );
                    File.Copy( sourceFile, targetFile, true );
                }
            }
        }

        // Delete files that should not be there.
        foreach ( var targetFile in Directory.GetFiles( targetPath, "*", SearchOption.AllDirectories ) )
        {
            if ( !expectedTargetFiles.Contains( targetFile ) )
            {
                console.WriteMessage( $"Deleting '{targetFile}'." );
                File.Delete( targetFile );
            }
        }
    }
}