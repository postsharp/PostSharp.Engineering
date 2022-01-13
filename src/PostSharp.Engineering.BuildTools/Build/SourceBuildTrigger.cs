using System.IO;

namespace PostSharp.Engineering.BuildTools.Build;

public class SourceBuildTrigger : IBuildTrigger
{
    public bool WatchChangesInDependencies { get; set; } = true; 
        
    public void GenerateTeamcityCode( BuildContext context, BuildConfigurationInfo configurationInfo, TextWriter writer )
    {
        writer.WriteLine(
            $@"
        vcs {{
            watchChangesInDependencies = {this.WatchChangesInDependencies.ToString().ToLowerInvariant()}
            branchFilter = ""+:<default>""
        }}        " );
    }
}