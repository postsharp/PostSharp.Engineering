// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Generic;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Docker;

public class DockerUbuntuImage : DockerImage
{
    private const string _nuGetPackagesDirectory = "/root/.nuget/packages";
    private const string _buildArtifactsDirectory = "/tmp/.build-artifacts";
    private const string _engineeringDataDirectory = "/root/.local/PostSharp.Engineering";

    public DockerUbuntuImage( string uri, string name ) : base( uri, name ) { }

    public override string GetAbsolutePath( params string[] components ) => "/" + this.GetRelativePath( components );

    public override string GetRelativePath( params string[] components ) => string.Join( "/", components );

    public override string NuGetPackagesDirectory => _nuGetPackagesDirectory;

    public override string DownloadedBuildArtifactsDirectory => _buildArtifactsDirectory;

    public override string EngineeringDataDirectory => _engineeringDataDirectory;

    public override string PowerShellCommand => "pwsh";

    public override DockerfileWriter CreateDockfileWriter( TextWriter writer ) => new Writer( writer, this );

    private class Writer( TextWriter textWriter, DockerUbuntuImage image ) : DockerfileWriter( textWriter, image )
    {
        public override void MakeDirectory( string s ) => this.WriteLine( $"RUN mkdir {s} -p" );

        public override void ReplaceLink( string target, string alias ) => this.WriteLine( $"RUN ln -s {target}  {alias}" );

        public override void RunPowerShellScript( List<string> commands )
            => this.WriteLine( "RUN pwsh -NoProfile -ExecutionPolicy Bypass -Command " + string.Join( ";\\\r\n", commands ) );

        public override void RunPowerShellFile( string fileAndArguments ) => this.WriteLine( "RUN " + fileAndArguments );

        public override void Rename( string oldName, string newName ) => this.WriteLine( $"RUN mv {this.EscapePath( oldName )} {this.EscapePath( newName )}" );
    }
}