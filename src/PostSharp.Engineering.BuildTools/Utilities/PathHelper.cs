﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Utilities;

public static class PathHelper
{
    public static string GetMetalamaApplicationDataDirectory() => Path.Combine( GetApplicationDataParentDirectory(), ".metalama" );
    
    public static string GetApplicationDataParentDirectory()
    {
        var applicationDataParentDirectory = Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData );

        if ( string.IsNullOrEmpty( applicationDataParentDirectory ) )
        {
            // This is a fallback for Ubuntu on WSL and other platforms that don't provide
            // the SpecialFolder.ApplicationData folder path.
            applicationDataParentDirectory = Environment.GetFolderPath( Environment.SpecialFolder.UserProfile );
        }

        if ( string.IsNullOrEmpty( applicationDataParentDirectory ) )
        {
            // This will always fail on platforms which don't provide the special folders being discovered above.
            // We need to find another locations on such platforms.
            throw new InvalidOperationException( "Failed to find application data parent directory." );
        }

        return applicationDataParentDirectory;
    }
}