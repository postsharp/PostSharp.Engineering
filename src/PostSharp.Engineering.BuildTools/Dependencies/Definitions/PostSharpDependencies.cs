// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;

namespace PostSharp.Engineering.BuildTools.Dependencies.Definitions;

[PublicAPI]
public static partial class PostSharpDependencies
{
    private const string _projectName = "PostSharp";

    /// <summary>
    /// The TeamCity project holding every PostSharp line. Each product of a line is a project of its own beneath it,
    /// named after the product and the version -- PostSharpGitHub_PostSharp20260,
    /// PostSharpGitHub_PostSharpDocumentation20260 -- so the lines do not share build configurations.
    /// </summary>
    private const string _parentProjectId = "PostSharpGitHub";
}
