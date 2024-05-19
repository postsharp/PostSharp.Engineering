// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.IO;

namespace PostSharp.Engineering.BuildTools.Docker;

public class DockerUbuntuImage : DockerImage
{
    public DockerUbuntuImage( string name ) : base( name ) { }

    public override DockerfileWriter CreateDockfileWriter( StreamWriter writer ) => new Writer( writer );

    private class Writer( StreamWriter writer ) : DockerfileWriter( writer )
    {
        public override void WritePrologue()
        {
            // Configure package caches.
            this.WriteLine( "ENV NUGET_PACKAGES=/root/.nuget/packages" );
            this.WriteLine( "VOLUME /root/.nuget/packages" );
            this.WriteLine( "VOLUME /tmp/.build-artifacts" );

            // Configuring bind mounts.
            this.WriteLine( "VOLUME /root/.local/PostSharp.Engineering" );
        }

        public override string GetPath( params string[] components ) => "/" + string.Join( "/", components );

        public override void MakeDirectory( string s )
        {
            this.WriteLine( $"RUN mkdir {s} -p" );
        }

        public override void ReplaceLink( string target, string alias )
        {
            this.WriteLine(
                $"""
                 RUN rm {alias} && \
                 ln -s {target}  {alias}
                 """ );
        }
    }
}