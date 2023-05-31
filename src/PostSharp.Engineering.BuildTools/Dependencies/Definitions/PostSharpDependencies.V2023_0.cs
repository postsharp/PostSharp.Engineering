// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static partial class PostSharpDependencies
{
    // ReSharper disable once InconsistentNaming

    public static class V2023_0
    {
        public static ProductFamily Family { get; } = new( "2023.0" );

        public static DependencyDefinition PostSharpDocumentation { get; } = new(
            Family,
            "PostSharp.Documentation",
            $"release/{Family.Version}",
            null,
            VcsProvider.GitHub,
            "PostSharp",
            false );
    }
}