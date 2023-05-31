// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static class TemplateDependencies
{
    public static ProductFamily Family { get; } = new( "2023.0" );
    
    // This is only used from the project template.
    public static DependencyDefinition MyProduct { get; } =
        new( Family, "PostSharp.Engineering.ProjectTemplate", $"develop/{Family.Version}", $"release/{Family.Version}", VcsProvider.GitHub, "NONE" );
}