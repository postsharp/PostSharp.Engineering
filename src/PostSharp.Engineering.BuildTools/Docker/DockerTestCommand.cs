// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;

namespace PostSharp.Engineering.BuildTools.Docker;

public class DockerTestCommand : DockerRunCommand
{
    protected override string GetCommand( BuildSettings settings, DockerImage image )
        => $"pwsh -c \" ./Configure.ps1 && ./Build.ps1 test {settings.WithoutLogo()}\"";
}