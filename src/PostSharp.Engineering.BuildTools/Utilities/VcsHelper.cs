// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;

namespace PostSharp.Engineering.BuildTools.Utilities
{
    public class VcsHelper
    {
        public static bool CheckNoChange( BuildContext context, CommonCommandSettings settings, string repo )
        {
            if ( !settings.Force )
            {
                if ( !ToolInvocationHelper.InvokeTool(
                         context.Console,
                         "git",
                         $"status --porcelain",
                         repo,
                         out var exitCode,
                         out var statusOutput )
                     || exitCode != 0 )
                {
                    return false;
                }

                if ( !string.IsNullOrWhiteSpace( statusOutput ) )
                {
                    context.Console.WriteError( $"There are non-committed changes in '{repo}' Use --force." );
                    context.Console.WriteImportantMessage( statusOutput );

                    return false;
                }
            }

            return true;
        }
    }
}