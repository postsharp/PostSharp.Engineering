// Copyright (c) SharpCrafters s.r.o. This file is not open source. It is released under a commercial
// source-available license. Please see the LICENSE.md file in the repository root for details.

using System;
using System.Runtime.InteropServices;

namespace PostSharp.Engineering.BuildTools.Utilities
{

    public static class FileSystemHelper
    {
        [DllImport( "kernel32.dll", EntryPoint="CreateHardLink", SetLastError = true, CharSet = CharSet.Unicode )]
        private static extern bool CreateHardLinkWin32( string newFileName, string exitingFileName, IntPtr securityAttributes );

        [DllImport( "kernel32.dll", EntryPoint = "CreateSymbolicLink", SetLastError = true, CharSet = CharSet.Unicode )]
        private static extern bool CreateSymbolicLinkWin32( string symlinkFileName, string targetFileName, uint dwFlags );

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments - the analyzer seems to fail for UNIX P/Invokes.
        [DllImport( "libc", EntryPoint = "link", SetLastError = true, CharSet = CharSet.Ansi )]
        private static extern int CreateHardLinkLinux( string oldpath, string newpath );

        [DllImport( "libc", EntryPoint = "symlink", SetLastError = true, CharSet = CharSet.Ansi )]
        private static extern int CreateSymbolicLinkLinux( string oldpath, string newpath );
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments - the analyzer seems to fail for UNIX P/Invokes.

        public static bool TryCreateHardLink( string existingFileName, string newFileName, out Exception exception )
        {
            bool hardLinkCreated;
            if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) )
            {
                hardLinkCreated = CreateHardLinkWin32( newFileName, existingFileName, IntPtr.Zero /* reserved, must be NULL */);
                exception = hardLinkCreated ? null : Marshal.GetExceptionForHR( Marshal.GetHRForLastWin32Error() );                
            }
            else
            {                
                hardLinkCreated = CreateHardLinkLinux( existingFileName, newFileName ) == 0;

                // TODO: We will need to change this so that FileSystemHelper is able to recognize file in use.
                exception = hardLinkCreated ? null : new Exception( $"link() call failed with error code: {Marshal.GetLastWin32Error()}" );
            }

            return hardLinkCreated;
        }

        public static bool TryCreateSymbolicLink( string existingFileName, string newFileName, out Exception exception )
        {
            bool symLinkCreated;
            if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) )
            {
                symLinkCreated = CreateSymbolicLinkWin32( newFileName, existingFileName, 0 );
                exception = symLinkCreated ? null : Marshal.GetExceptionForHR( Marshal.GetHRForLastWin32Error() );
            }
            else
            {
                symLinkCreated = CreateSymbolicLinkLinux( existingFileName, newFileName ) == 0;

                // We will need to change this so that FileSystemHelper is able to recognize file in use.
                exception = symLinkCreated ? null : new Exception( $"symlink() call failed with error code: {Marshal.GetLastWin32Error()}" );
            }

            return symLinkCreated;
        }
    }
}
