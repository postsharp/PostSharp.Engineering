// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.IO;

namespace PostSharp.Engineering.BuildTools.Docker;

public abstract class DockerImage
{
    protected DockerImage( string name ) 
    {
        this.Name = name;
    }

    public string Name { get; }

    public abstract DockerfileWriter CreateDockfileWriter( StreamWriter writer );
}