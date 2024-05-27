// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Generic;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Docker;

public abstract class DockerImageComponent
{
    protected DockerImageComponent( string name )
    {
        this.Name = name;
    }

    public string Name { get; }

    public abstract int Order { get; }

    public abstract void AppendToDockerfile( DockerfileWriter writer );

    protected virtual IEnumerable<DockerImageComponent> GetPrerequisites() => [];

    public void AddPrerequisites( Dictionary<string, DockerImageComponent> components )
    {
        foreach ( var child in this.GetPrerequisites() )
        {
            if ( components.TryAdd( child.Name, child ) )
            {
                child.AddPrerequisites( components );
            }
        }
    }
}