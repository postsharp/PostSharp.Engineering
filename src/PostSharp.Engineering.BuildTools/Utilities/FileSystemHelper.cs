// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PostSharp.Engineering.BuildTools.Utilities;

public static class FileSystemHelper
{
    public static string GetFinalPath( string path )
    {
        using ( var fileStream = new FileStream( path, FileMode.Open, FileAccess.Read ) )
        {
            var fileHandle = fileStream.SafeFileHandle;
            var buffer = new StringBuilder( 1024 );
            var bufferSize = (uint) buffer.Capacity;
            var result = GetFinalPathNameByHandle( fileHandle.DangerousGetHandle(), buffer, bufferSize, 0 );

            if ( result > bufferSize )
            {
                // The buffer is too small.
                buffer.Capacity = (int) result;
                result = GetFinalPathNameByHandle( fileHandle.DangerousGetHandle(), buffer, result, 0 );
            }

            if ( result == 0 )
            {
                Marshal.ThrowExceptionForHR( Marshal.GetHRForLastWin32Error() );
            }

            var realPath = buffer.ToString();

            const string prefix = @"\\?\";

            if ( realPath.StartsWith( prefix, StringComparison.Ordinal ) )
            {
                realPath = realPath.Substring( prefix.Length );
            }

            return realPath;
        }
    }

    [DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true )]
    private static extern uint GetFinalPathNameByHandle(
        IntPtr hFile,
        [MarshalAs( UnmanagedType.LPTStr )] StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags );
}