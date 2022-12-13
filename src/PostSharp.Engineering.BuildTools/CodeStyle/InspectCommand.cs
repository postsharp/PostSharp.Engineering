// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using System.IO;

namespace PostSharp.Engineering.BuildTools.CodeStyle;

internal class InspectCommand : ResharperCommand
{
    protected override string Title => "Inspecting the code";

    // ReSharper disable once StringLiteralTypo
    protected override string GetCommand( BuildContext context, Solution solution )
    {
        var outputPath = Path.Combine( context.RepoDirectory, "artifacts", "logs", "CodeIssues.xml" );

        context.Console.WriteImportantMessage( $"Writing resul to '{outputPath}'." );

        return $"inspectcode \"{Path.Combine( context.RepoDirectory, solution.SolutionPath )}\" --build -o={outputPath} --severity=WARNING -f=Xml";
    }

    protected override void OnSuccessfulExecution( BuildContext context, Solution solution )
    {
        var outputPath = Path.Combine( context.RepoDirectory, "artifacts", "logs", "CodeIssues.xml" );

        ProcessInspectOutputCommand.ExecuteImpl( context, new ProcessInspectOutputCommandSettings() { Path = outputPath } );
    }
}