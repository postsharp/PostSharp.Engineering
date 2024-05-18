// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.IO;

namespace PostSharp.Engineering.BuildTools.Docker;

internal class MicrosoftAptPackageSource : DockerImageComponent
{
    public MicrosoftAptPackageSource() : base( "packages-microsoft-prod" ) { }

    public override int Order => 0;

    public override void AppendToDockerfile( StreamWriter writer )
    {
        writer.WriteLine(
            """
            RUN apt-get update && \
            apt-get install -y wget apt-transport-https software-properties-common && \
            wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
            dpkg -i packages-microsoft-prod.deb && \
            apt-get update
            """ );
    }
}