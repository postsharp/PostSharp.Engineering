// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Generic;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Docker;

internal class AptPackageImageComponent : DockerImageComponent
{
    public AptPackageImageComponent( string name, int order = 10 ) : base( name )
    {
        this.Order = order;
    }

    public override int Order { get; }

    public override void AppendToDockerfile( DockerfileWriter writer )
    {
        writer.WriteLine( $"RUN apt-get install -y {this.Name}" );
    }

    protected override IEnumerable<DockerImageComponent> GetPrerequisites() => [DockerImageComponents.MicrosoftAptPackageSource];
}