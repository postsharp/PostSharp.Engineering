// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Docker;

// ReSharper disable InconsistentNaming
public static class DockerImages
{
    public static DockerImage DotNetSdk_8_0_204_Jammy { get; } = new DockerUbuntuImage( "mcr.microsoft.com/dotnet/sdk:8.0.204-jammy", "jammy" );

    public static DockerImage WindowsServerCore { get; } = new DockerWindowsImage(
        "mcr.microsoft.com/dotnet/framework/sdk:4.8-windowsservercore-ltsc2019",
        "windowsservercore" );

    public static ImmutableArray<DockerImage> All { get; } = [DotNetSdk_8_0_204_Jammy, WindowsServerCore];
}