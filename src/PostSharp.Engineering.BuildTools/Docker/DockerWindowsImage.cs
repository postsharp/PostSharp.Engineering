// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using System.Collections.Generic;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Docker;

public class DockerWindowsImage : DockerImage
{
    public DockerWindowsImage( string uri, string name, string operatingSystemName ) : base(
        uri,
        name,
        new BuildAgentRequirements( new BuildAgentRequirement( "env.BuildAgentType", "DockerWindowsStandard" ) ) ) { }

    public override string EngineeringDataDirectory => @"C:\Users\ContainerAdministrator\AppData\Roaming\PostSharp.Engineering";

    public override string PowerShellCommand => "powershell";

    public override DockerfileWriter CreateDockfileWriter( TextWriter writer ) => new Writer( writer, this );

    public override string GetAbsolutePath( params string[] components ) => "C:\\" + this.GetRelativePath( components );

    public override string GetRelativePath( params string[] components ) => string.Join( "\\", components );

    public override string NuGetPackagesDirectory => @"C:\Users\ContainerAdministrator\.nuget\packages";

    public override string DownloadedBuildArtifactsDirectory => @"C:\Users\ContainerAdministrator\.build-artifacts";

    private class Writer( TextWriter textWriter, DockerWindowsImage image ) : DockerfileWriter( textWriter, image )
    {
        public override void WritePrologue( BuildSettings settings )
        {
            // First write our own installation components so they as as independent as possible from
            // the rest of the prolog, to improve caching.
            this.WriteLine(
                """
                # Install Chocolatey
                RUN powershell -NoProfile -ExecutionPolicy Bypass -Command \
                    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; \
                    Set-ExecutionPolicy Bypass -Scope Process -Force; \
                    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; \
                    Invoke-WebRequest -Uri https://chocolatey.org/install.ps1 -OutFile install.ps1; \
                    powershell.exe -NoProfile -ExecutionPolicy Bypass -File install.ps1; \
                    Remove-Item -Force install.ps1

                # Install Git using Chocolatey
                RUN choco install git -y
                """ );

            base.WritePrologue( settings );
        }

        public override void MakeDirectory( string s )
        {
            this.WriteLine( $"RUN md {this.EscapePath( s )}" );
        }

        public override void ReplaceLink( string target, string alias )
        {
            this.WriteLine( $"RUN New-Item -ItemType SymbolicLink -Path {alias} -Target {target}" );
        }

        public override void RunPowerShellFile( string fileAndArguments ) => this.WriteLine( "RUN powershell " + fileAndArguments );

        public override void RunPowerShellScript( List<string> commands ) => this.WriteLine( "RUN " + string.Join( ";\\\r\n", commands ) );

        public override void Rename( string oldName, string newName ) => this.WriteLine( $"RUN ren {this.EscapePath( oldName )} {this.EscapePath( newName )}" );
    }
}