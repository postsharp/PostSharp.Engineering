// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration.Model.Arguments;

public abstract class TeamCityBuildConfigurationParameter
{
    public string Name { get; }

    protected TeamCityBuildConfigurationParameter( string name )
    {
        this.Name = name;
    }

    public abstract string GenerateTeamCityCode();
}