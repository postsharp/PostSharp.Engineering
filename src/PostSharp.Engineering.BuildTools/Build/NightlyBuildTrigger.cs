using System.IO;

namespace PostSharp.Engineering.BuildTools.Build;

public class NightlyBuildTrigger : IBuildTrigger
{
    public void GenerateTeamcityCode( BuildContext context, BuildConfigurationInfo configurationInfo, TextWriter writer )
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