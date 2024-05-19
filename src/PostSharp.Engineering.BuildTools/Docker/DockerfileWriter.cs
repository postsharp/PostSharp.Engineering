// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Globalization;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Docker;

public abstract class DockerfileWriter : IDisposable
{
    private readonly StreamWriter _streamWriter;

    protected DockerfileWriter( StreamWriter streamWriter )
    {
        this._streamWriter = streamWriter;
    }

    public abstract void WritePrologue();

    public void WriteLine( string s )
    {
        this._streamWriter.WriteLine( s );
    }

    public void WriteLine( FormattableString s )
    {
        this._streamWriter.WriteLine( s.ToString( CultureInfo.InvariantCulture ) );
    }

    public abstract string GetPath( params string[] components );

    public void Dispose()
    {
        this._streamWriter.Dispose();
    }

    public void Close()
    {
        this._streamWriter.Close();
    }

    public abstract void MakeDirectory( string s );

    public abstract void ReplaceLink( string target, string alias );
}