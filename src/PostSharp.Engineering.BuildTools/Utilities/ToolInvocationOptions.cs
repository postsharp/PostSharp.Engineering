// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Utilities;

public record ToolInvocationOptions(
    ImmutableDictionary<string, string?>? EnvironmentVariables = null,
    bool Silent = false,
    ImmutableArray<string> BlockedEnvironmentVariables = default )
{
    // Some environment variables are set by the Microsoft.Build package and must not be passed to the child process.
    public ImmutableArray<string> BlockedEnvironmentVariables { get; init; } =
        BlockedEnvironmentVariables.IsDefault ? ImmutableArray.Create( "DOTNET_ROOT_X64", "MSBUILD_EXE_PATH", "MSBuildSDKsPath" ) : BlockedEnvironmentVariables;
}