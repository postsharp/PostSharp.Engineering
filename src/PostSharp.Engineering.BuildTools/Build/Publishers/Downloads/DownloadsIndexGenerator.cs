// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Build.Publishers.Downloads;

[PublicAPI]
public static class DownloadsIndexGenerator
{
    private static string FormatTime( DateTime time ) => time.ToString( @"yyyy-MM-ddTHH\:mm\:ss.fffffffzzz", CultureInfo.InvariantCulture );

    private static string FormatString( string? value )
        => value == null
            ? ""
            : $@"<![CDATA[
            {value}
          ]]>";
    
    public static void Generate( DownloadsIndex index, string directory )
    {
        var createdAt = FormatTime( index.Folder.CreatedAt );
        var partialIndex = index.IsPartialIndex ? @" PartialIndex=""true""" : "";

        var files = string.Join(
            Environment.NewLine,
            index.Folder.Files.Select(
                f => $@"<File Name=""{f.Name}"" CreatedAt=""{FormatTime( f.CreatedAt )}"">
        <Description>
          {FormatString( f.Description )}
        </Description>
        <Instructions>
          {FormatString( f.Instructions )}
        </Instructions>
      </File>" ) );

        var content = @$"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Index xmlns=""http://www.postsharp.net/schemas/downloads.xsd"">
  <Folder Name=""{index.Folder.Name}"" Order=""{index.Folder.Order}"" CreatedAt=""{createdAt}""{partialIndex}>
    <Description>
      {FormatString( index.Folder.Description )}
    </Description>
    <LongDescription>
      {FormatString( index.Folder.LongDescription )}
    </LongDescription>
    <Instructions>
      {FormatString( index.Folder.Instructions )}
    </Instructions>
    <Files>
      {files}
    </Files>
  </Folder>

</Index>
";
        
        var indexFilePath = Path.Combine( directory, index.FileName );
        
        File.WriteAllText( indexFilePath, content );
    }
}