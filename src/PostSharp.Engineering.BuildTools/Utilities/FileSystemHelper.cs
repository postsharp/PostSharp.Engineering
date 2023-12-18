// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.IO;

namespace PostSharp.Engineering.BuildTools.Utilities;

public static class FileSystemHelper
{
    public static string GetFinalPath( string path )
    {
        var linkTarget = File.ResolveLinkTarget( path, returnFinalTarget: true );

        return linkTarget?.FullName ?? Path.GetFullPath( path );
    }
}