// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model.Arguments;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration.Model.BuildSteps;

public class TeamCityEngineeringCommandBuildStep : TeamCityPowerShellBuildStep
{
    private static string GetCustomArgumentsParameterName( string objectName ) => $"{objectName}Arguments";

    public TeamCityEngineeringCommandBuildStep(
        string objectName,
        string name,
        string command,
        string? arguments = null,
        bool areCustomArgumentsAllowed = false,
        bool useDocker = false ) : base(
        name,
        "Build.ps1",
        $"{(useDocker ? "docker " : "")}{command}{(arguments == null ? "" : $" {arguments}")}{(!areCustomArgumentsAllowed ? "" : $" %{GetCustomArgumentsParameterName( objectName )}%")}" )
    {
        if ( areCustomArgumentsAllowed )
        {
            this.BuildConfigurationParameters =
            [
                new TeamCityTextBuildConfigurationParameter(
                    GetCustomArgumentsParameterName( objectName ),
                    $"{name} Arguments",
                    $"Arguments to append to the '{name}' build step.",
                    allowEmpty: true )
            ];
        }
    }
}