// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.IO;
using System.Net.Http;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Utilities;

public static class HttpExtensions
{
    public static Stream GetStream( this HttpClient client, string uri )
        => client.GetStreamAsync( uri, ConsoleHelper.CancellationToken ).ConfigureAwait( false ).GetAwaiter().GetResult();
    
    public static string ReadAsString( this HttpContent content )
        => content.ReadAsStringAsync( ConsoleHelper.CancellationToken ).ConfigureAwait( false ).GetAwaiter().GetResult();

    public static XDocument ReadAsXDocument( this HttpContent content ) => XDocument.Load( content.ReadAsStream() );
}