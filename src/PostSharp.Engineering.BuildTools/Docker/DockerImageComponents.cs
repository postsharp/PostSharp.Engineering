// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Docker;

public static class DockerImageComponents
{
    internal static MicrosoftAptPackageSource MicrosoftAptPackageSource => new();

    public static DockerImageComponent DotNetSdk60 => new AptPackageImageComponent( "dotnet-sdk-6.0", 100 );
}