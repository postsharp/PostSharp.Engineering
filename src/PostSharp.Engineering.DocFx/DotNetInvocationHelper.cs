// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Utilities;

namespace PostSharp.Engineering.DocFx;

internal static class DotNetInvocationHelper
{
    public static bool Run( BuildContext context, string command, string arguments, ToolInvocationOptions? options = null )
        => ToolInvocationHelper.InvokeTool( context.Console, "dotnet", $"{command} {arguments}", context.RepoDirectory, options );
}