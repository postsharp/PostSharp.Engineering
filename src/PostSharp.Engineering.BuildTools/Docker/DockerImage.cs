// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.IO;

namespace PostSharp.Engineering.BuildTools.Docker;

public abstract class DockerImage
{
    protected DockerImage( string uri, string name )
    {
        this.Uri = uri;
        this.Name = name;
    }

    public string Uri { get; }

    public string Name { get; }

    public abstract string GetAbsolutePath( params string[] components );

    public abstract string GetRelativePath( params string[] components );

    public abstract string NuGetPackagesDirectory { get; }

    public abstract string DownloadedBuildArtifactsDirectory { get; }

    public abstract string EngineeringDataDirectory { get; }

    public abstract string PowerShellCommand { get; }

    public abstract DockerfileWriter CreateDockfileWriter( TextWriter writer );
}