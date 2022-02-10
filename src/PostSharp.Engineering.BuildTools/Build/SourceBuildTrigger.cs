using System.IO;

namespace PostSharp.Engineering.BuildTools.Build;

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
        }}        " );
    }
}