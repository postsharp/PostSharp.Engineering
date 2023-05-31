// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static class DevelopmentDependencies
{
    public static ProductFamily ProductFamily { get; } = new( "2023.2" );

    private static readonly string _devBranch = $"develop/{ProductFamily.Version}";
    private static readonly string _releaseBranch = $"release/{ProductFamily.Version}";

    public static DependencyDefinition PostSharpEngineering { get; } = new(
        ProductFamily,
        "PostSharp.Engineering",
        _devBranch,
        _releaseBranch,
        VcsProvider.GitHub,
        "postsharp" )
    {
        GenerateSnapshotDependency = false,

        // We always use the debug build for engineering.
        CiBuildTypes = new ConfigurationSpecific<string>(
            "Internal_PostSharpEngineering_DebugBuild",
            "Internal_PostSharpEngineering_DebugBuild",
            "Internal_PostSharpEngineering_DebugBuild" )
    };
}