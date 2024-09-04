// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build.Triggers;

/// <summary>
/// Generates a build trigger that triggers the build daily at 22:00 for the default branch.
/// </summary>
public class NightlyBuildTrigger : IBuildTrigger
{
    public int Hour { get; }
    
    public int Minute { get; }

    public bool WithPendingChangesOnly { get; }

    public NightlyBuildTrigger( int hour, bool withPendingChangesOnly )
    {
        this.Hour = hour;
        this.WithPendingChangesOnly = withPendingChangesOnly;
    }
    
    public NightlyBuildTrigger( int hour, int minute, bool withPendingChangesOnly )
    {
        this.Hour = hour;
        this.Minute = minute;
        this.WithPendingChangesOnly = withPendingChangesOnly;
    }

    public void GenerateTeamcityCode( TextWriter writer, string branchFilter )
    {
        writer.WriteLine(
            @$"        schedule {{
            schedulingPolicy = daily {{
                hour = {this.Hour}
                minute = {this.Minute}
            }}
            branchFilter = ""{branchFilter}""
            triggerBuild = always()
            withPendingChangesOnly = {this.WithPendingChangesOnly.ToString().ToLowerInvariant()}
        }}" );
    }
}