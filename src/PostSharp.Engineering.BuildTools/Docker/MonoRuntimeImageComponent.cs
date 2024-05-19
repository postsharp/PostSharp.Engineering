// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Docker;

internal class MonoRuntimeImageComponent : DockerImageComponent
{
    public MonoRuntimeImageComponent() : base( "mono-complete" ) { }

    public override int Order => 0;

    public override void AppendToDockerfile( DockerfileWriter writer )
    {
        // Set environment variables to avoid interactive prompts during installation
        writer.WriteLine( "ENV DEBIAN_FRONTEND=noninteractive" );

        // Install required packages and Mono repository key
        writer.WriteLine(
            """
            RUN apt-get update && \
            apt-get install -y gnupg ca-certificates && \
            apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
            """ );

        // Add the Mono repository to sources list
        writer.WriteLine(
            """RUN echo "deb https://download.mono-project.com/repo/ubuntu stable-focal main" | tee /etc/apt/sources.list.d/mono-official-stable.list""" );

        writer.WriteLine(
            """
            RUN apt-get update && \
                apt-get install -y mono-complete
            """ );
    }
}