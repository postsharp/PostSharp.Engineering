// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.IO;

namespace PostSharp.Engineering.BuildTools.Utilities;

public static class StreamExtensions
{
    public static void CopyTo( this Stream from, Stream to )
        => from.CopyToAsync( to, ConsoleHelper.CancellationToken ).ConfigureAwait( false ).GetAwaiter().GetResult();
}