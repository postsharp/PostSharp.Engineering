using System.IO;

namespace PostSharp.Engineering.BuildTools.Build;

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