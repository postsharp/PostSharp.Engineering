namespace PostSharp.Engineering.BuildTools.Docker;

// ReSharper disable InconsistentNaming

public static class DockerImages
{
    public static DockerImage DotNetSdk_8_0_204_Jammy { get; } = new DockerUbuntuImage( "mcr.microsoft.com/dotnet/sdk:8.0.204-jammy" );

    // Building on Windows Server Nano does not work yet.
    public static DockerImage DotNetSdk_8_0_300_NanoServer { get; } = new DockerWindowsImage( "mcr.microsoft.com/dotnet/sdk:8.0.300-nanoserver-ltsc2022" );
}