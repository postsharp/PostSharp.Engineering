// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public record CiLatestBuildOfBranch( string Name ) : ICiBuildSpec;