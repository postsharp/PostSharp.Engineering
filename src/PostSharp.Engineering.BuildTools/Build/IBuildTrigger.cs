using System.IO;

namespace PostSharp.Engineering.BuildTools.Build;

public interface IBuildTrigger
{
    void GenerateTeamcityCode( TextWriter writer );
}