// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Docker;

public abstract class DockerfileWriter : IDisposable
{
    public TextWriter TextWriter { get; }

    public DockerImage Image { get; }

    protected DockerfileWriter( TextWriter streamTextWriter, DockerImage image )
    {
        this.TextWriter = streamTextWriter;
        this.Image = image;
    }

    public string EscapePath( string s ) => "\"" + s.Replace( "\\", "\\\\", StringComparison.Ordinal ) + "\"";

    public virtual void WritePrologue()
    {
        this.WriteLine( $"ENV NUGET_PACKAGES={this.EscapePath( this.Image.NuGetPackagesDirectory )}" );

        ConfigureVolume( this.Image.NuGetPackagesDirectory );
        ConfigureVolume( this.Image.DownloadedBuildArtifactsDirectory );

        void ConfigureVolume( string directory )
        {
            this.WriteLine( $"VOLUME {this.EscapePath( directory )}" );
        }
    }

    public void WriteLine( string s )
    {
        this.TextWriter.WriteLine( s );
    }

    public void Dispose()
    {
        this.TextWriter.Dispose();
    }

    public void Close()
    {
        this.TextWriter.Close();
    }

    public abstract void MakeDirectory( string s );

    public abstract void ReplaceLink( string target, string alias );

    public abstract void RunPowerShellFile( string fileAndArguments );

    public abstract void Rename( string oldName, string newName );

    public abstract void RunPowerShellScript( List<string> commands );
}