using PostSharp.Engineering.BuildTools.Build.Model;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build.Triggers;

/// <summary>
/// Generates a build trigger that triggers the build when the source code has changed in the default branch.
/// </summary>
public class SourceBuildTrigger : IBuildTrigger
{
    public bool WatchChangesInDependencies { get; set; } = true;

    public void GenerateTeamcityCode( TextWriter writer )
    {
        writer.WriteLine(
            $@"
        vcs {{
            watchChangesInDependencies = {this.WatchChangesInDependencies.ToString().ToLowerInvariant()}
            branchFilter = ""+:<default>""
            triggerRules = ""-:comment=<<VERSION_BUMP>>:**""
        }}        " );
    }
}