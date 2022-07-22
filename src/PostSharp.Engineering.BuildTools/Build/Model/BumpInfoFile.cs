using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace PostSharp.Engineering.BuildTools.Build.Model;

class BumpInfoFile
{
    public Dictionary<string, Version> Dependencies { get; init; } = new Dictionary<string, Version>();

    public BumpInfoFile( Dictionary<string, Version> dependencies ) { this.Dependencies = dependencies; }

    public BumpInfoFile() { }

    public static BumpInfoFile? FromText( string text )
    {
        try
        {
            return JsonConvert.DeserializeObject<BumpInfoFile>( text );
        }
        catch
        {
            return null;
        }
    }

    public override string ToString() => JsonConvert.SerializeObject( this );
}