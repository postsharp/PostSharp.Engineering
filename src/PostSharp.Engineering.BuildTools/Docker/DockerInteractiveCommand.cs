// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build;

namespace PostSharp.Engineering.BuildTools.Docker;

[UsedImplicitly]
public class DockerInteractiveCommand : DockerRunCommand
{
    protected override string GetCommand( DockerSettings settings, DockerImage image )
    {
        if ( settings.SkipConfigure )
        {
            return $"{image.PowerShellCommand}";
        }
        else
        {
            return $"{image.PowerShellCommand} -NoExit -c \" ./dependencies/ConfigureContainer.ps1; \"";    
        }
    }

    protected override string[] Options => ["-i"];
}