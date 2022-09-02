// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public enum DependencyConfigurationOrigin
{
    /// <summary>
    /// Unknown origin.
    /// </summary>
    Unknown,

    /// <summary>
    /// Default value defined in source code.
    /// </summary>
    Default,

    /// <summary>
    /// Overridden value using the command-line tool.
    /// </summary>
    Override,

    /// <summary>
    /// Transitive from a parent dependency.
    /// </summary>
    Transitive
}