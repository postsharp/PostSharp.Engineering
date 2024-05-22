// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;

[PublicAPI]
public sealed record BuildAgentRequirements
{
    private BuildAgentRequirements( ImmutableDictionary<string, string> parameters )
    {
        this.Parameters = parameters;
    }

    public static BuildAgentRequirements SelfHosted( string name ) => new( ImmutableDictionary.Create<string, string>().Add( "env.BuildAgentType", name ) );

    public static BuildAgentRequirements JetBrainsHosted( string name )
        => new( ImmutableDictionary.Create<string, string>().Add( "teamcity.agent.name", name ) );

    public ImmutableDictionary<string, string> Parameters { get; init; }
}