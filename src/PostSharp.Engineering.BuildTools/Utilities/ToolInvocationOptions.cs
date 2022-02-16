using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Utilities;

public record ToolInvocationOptions( ImmutableDictionary<string, string?>? EnvironmentVariables = null, bool Silent = false );