using System.IO;

namespace PostSharp.Engineering.BuildTools.Build;

public interface IBuildTrigger
{
    void GenerateTeamcityCode( BuildContext context, BuildConfigurationInfo configurationInfo, TextWriter writer );
}