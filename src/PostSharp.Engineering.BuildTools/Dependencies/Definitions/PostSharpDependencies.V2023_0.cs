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
        public static ProductFamily ProductFamily { get; } = new( "2023.0" );

        private static readonly string _devBranch = $"release/{ProductFamily.Version}";
        private const string? _releaseBranch = null;

        public static DependencyDefinition PostSharpDocumentation { get; } = new(
            ProductFamily,
            "PostSharp.Documentation",
            _devBranch,
            _releaseBranch,
            VcsProvider.GitHub,
            "PostSharp",
            false );
    }
}