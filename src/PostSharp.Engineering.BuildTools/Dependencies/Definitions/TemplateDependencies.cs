// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static class TemplateDependencies
{
    public static ProductFamily ProductFamily { get; } = new( "2023.0" );
    
    private static readonly string _devBranch = $"develop/{ProductFamily.Version}";
    private static readonly string _releaseBranch = $"release/{ProductFamily.Version}";
    
    // This is only used from the project template.
    public static DependencyDefinition MyProduct { get; } =
        new( ProductFamily, "PostSharp.Engineering.ProjectTemplate", _devBranch, _releaseBranch, VcsProvider.GitHub, "NONE" );
}