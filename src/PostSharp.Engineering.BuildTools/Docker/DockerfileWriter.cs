using System;
using System.Globalization;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Docker;

public abstract class DockerfileWriter : IDisposable
{
    public StreamWriter StreamWriter { get; }

    protected DockerfileWriter( StreamWriter streamWriter ) {
        this.StreamWriter = streamWriter;
    }

    public abstract void WritePrologue();

    public void WriteLine( string s )
    {
        this.StreamWriter.WriteLine(s);
    }
    
    public void WriteLine( FormattableString s )
    {
        this.StreamWriter.WriteLine(s.ToString(CultureInfo.InvariantCulture));
    }

    public abstract string GetPath( params string[] components );
    
    public void Dispose()
    {
        this.StreamWriter.Dispose();
    }

    public void Close()
    {
        this.StreamWriter.Close();
    }

    public abstract void MakeDirectory( string s );

    public abstract void ReplaceLink( string target, string alias );

}