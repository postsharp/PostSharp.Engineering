// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build.Triggers;

/// <summary>
/// Generates a build trigger that triggers the build daily at 22:00 for the default branch.
/// </summary>
public class NightlyBuildTrigger : IBuildTrigger
{
    public void GenerateTeamcityCode( TextWriter writer )
    {
        writer.WriteLine(
            @"
        schedule {
            schedulingPolicy = daily {
                hour = 22
            }
            branchFilter = ""+:<default>""
            triggerBuild = always()
        }
" );
    }
}