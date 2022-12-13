// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using System.IO;

namespace PostSharp.Engineering.BuildTools.CodeStyle
{
    /// <summary>
    /// Formats the source code of the current repo by applying the standard code style (running JetBrains cleanupcode command).
    /// </summary>
    internal class FormatCommand : ResharperCommand
    {
        protected override string Title => "Reformatting the code";

        protected override string GetCommand( BuildContext context, Solution solution )
        {
            var command = $"cleanupcode --profile:Custom \"{Path.Combine( context.RepoDirectory, solution.SolutionPath )}\"";

            // Sets MSBuild property 'FormattingCode' to true during code formatting, this allows for exclusion of specific projects from Compile target when code formatting is run.
            command += $" --properties:FormattingCode=true";

            return command;

        }
    }
}