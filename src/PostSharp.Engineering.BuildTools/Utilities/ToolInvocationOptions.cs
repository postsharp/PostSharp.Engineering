// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Utilities;

public record ToolInvocationOptions( ImmutableDictionary<string, string?>? EnvironmentVariables = null, bool Silent = false );