// Copyright (c) SharpCrafters s.r.o.See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration.Model.Arguments;

public class TeamCityBuildConfigurationParameter : TeamCityBuildConfigurationParameterBase
{
    public string Value { get; }

    public TeamCityBuildConfigurationParameter( string name, string value ) : base( name )
    {
        this.Value = value;
    }

    public override string GenerateTeamCityCode()
        => @$"        param(""{this.Name}"", ""{this.Value}"")";
}