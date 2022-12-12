using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using System.IO;

namespace PostSharp.Engineering.BuildTools.CodeStyle;

public class InspectCommand : ResharperCommand
{
    protected override string Title => "Inspecting the code";

    protected override string GetCommand( BuildContext context, Solution solution )
        => $"inspectcode \"{Path.Combine( context.RepoDirectory, solution.SolutionPath )}\"";

}