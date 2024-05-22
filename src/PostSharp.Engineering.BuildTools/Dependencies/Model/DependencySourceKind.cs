// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public enum DependencySourceKind
    {
        /// <summary>
        /// Means that the package source is a NuGet feed, which should be, typically, registered in nuget.config.
        /// </summary>
        Feed,

        /// <summary>
        /// Means that the package source is the local artefacts directory of a local repo.
        /// </summary>
        Local,

        /// <summary>
        /// Means that the package source is a build artifact set of a continuous integration build.
        /// </summary>
        BuildServer,

        /// <summary>
        /// Means that the artefacts have been restored under the .\dependencies directory.
        /// </summary>
        RestoredDependency
    }
}