// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration.Model.BuildSteps;

public class TeamCityPowerShellBuildStep : TeamCityBuildStep
{
    public string Name { get; }

    public string ScriptPath { get; }

    public string ScriptArguments { get; }

    public TeamCityPowerShellBuildStep( string name, string scriptPath, string scriptArguments )
    {
        this.Name = name;
        this.ScriptPath = scriptPath;
        this.ScriptArguments = scriptArguments;
    }

    public override string GenerateTeamCityCode()
        => $@"        powerShell {{
            name = ""{this.Name}""
            scriptMode = file {{
                path = ""{this.ScriptPath}""
            }}
            noProfile = false
            param(""jetbrains_powershell_scriptArguments"", ""{this.ScriptArguments}"")
        }}";
}