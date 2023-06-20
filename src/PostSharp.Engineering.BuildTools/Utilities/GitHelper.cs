// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Utilities;

internal static class GitHelper
{
    private static bool TryAddOrigin( BuildContext context, string branch )
    {
        // Add origin/<branch> branch to the list of currently tracked branches because local repository may be initialized with only the default branch.
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"remote set-branches --add origin {branch}",
                context.RepoDirectory ) )
        {
            return false;
        }

        return true;
    }

    public static bool TryFetch( BuildContext context, string? branch, out bool remoteNotFound )
    {
        remoteNotFound = false;
        
        if ( branch != null && !TryAddOrigin( context, branch ) )
        {
            return false;
        }

        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"fetch",
                context.RepoDirectory,
                out var fetchExitCode,
                out var fetchOutput ) )
        {
            remoteNotFound = branch != null
                             && fetchExitCode == 128
                             && fetchOutput.Trim().Equals( $"fatal: couldn't find remote ref refs/heads/{branch}", StringComparison.Ordinal );

            if ( remoteNotFound )
            {
                context.Console.WriteMessage( fetchOutput );
            }
            else
            {
                context.Console.WriteError( fetchOutput );
            }

            return false;
        }

        return true;
    }

    public static bool TryCheckoutAndPull( BuildContext context, string branch, out bool remoteNotFound )
    {
        if ( !TryFetch( context, branch, out remoteNotFound ) )
        {
            return false;
        }

        // Switch to the <branch> branch before we do merge.
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"checkout {branch}",
                context.RepoDirectory ) )
        {
            return false;
        }

        // Pull remote changes
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"pull origin {branch}",
                context.RepoDirectory ) )
        {
            return false;
        }

        return true;
    }

    public static bool TryCreateBranch( BuildContext context, string branch )
    {
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"checkout -b {branch}",
                context.RepoDirectory ) )
        {
            return false;
        }

        if ( !TryAddOrigin( context, branch ) )
        {
            return false;
        }

        return true;
    }

    public static bool TryGetCurrentBranch( BuildContext context, [NotNullWhen( true )] out string? currentBranch )
        => TryGetCurrentBranch( context.Console, context.RepoDirectory, out currentBranch );

    public static bool TryGetCurrentBranch( ConsoleHelper console, string repoDirectory, [NotNullWhen( true )] out string? currentBranch )
    {
        ToolInvocationHelper.InvokeTool(
            console,
            "git",
            $"branch --show-current",
            repoDirectory,
            out var gitExitCode,
            out var gitOutput );

        if ( gitExitCode != 0 )
        {
            console.WriteError( gitOutput );
            currentBranch = null;

            return false;
        }

        currentBranch = gitOutput.Trim();

        return true;
    }

    public static bool TryGetCurrentCommitHash( BuildContext context, [NotNullWhen( true )] out string? currentCommitHash )
    {
        ToolInvocationHelper.InvokeTool(
            context.Console,
            "git",
            $"rev-parse HEAD",
            context.RepoDirectory,
            out var gitExitCode,
            out var gitOutput );

        if ( gitExitCode != 0 )
        {
            context.Console.WriteError( gitOutput );
            currentCommitHash = null;

            return false;
        }

        currentCommitHash = gitOutput.Trim();

        return true;
    }

    public static bool TryGetCommitsCount( BuildContext context, string from, string to, out int count, string options = "" )
    {
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"rev-list --count \"{from}..{to}\" {options}",
                context.RepoDirectory,
                out _,
                out var output ) )
        {
            context.Console.WriteError( output );
            count = -1;
            
            return false;
        }

        count = int.Parse( output, CultureInfo.InvariantCulture );

        return true;
    }
    
    public static bool TryGetRemoteBranchesCount( BuildContext context, BaseBuildSettings settings, string filter, out int count )
    {
        count = -1;
        
        if ( !TryGetOriginUrl( context, settings, out var originUrl ) )
        {
            return false;
        }
        
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"ls-remote {originUrl} {filter}",
                context.RepoDirectory,
                out _,
                out var output ) )
        {
            context.Console.WriteError( output );

            return false;
        }

        count = output.Split( "\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries ).Length;

        return true;
    }

    public static bool TryCommitAll( BuildContext context, string message )
    {
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"commit -am \"{message}\"",
                context.RepoDirectory ) )
        {
            return false;
        }

        return true;
    }

    public static bool TryCommitMerge( BuildContext context )
    {
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                "commit --no-edit",
                context.RepoDirectory ) )
        {
            return false;
        }

        return true;
    }

    public static bool TryMerge( BuildContext context, string sourceBranch, string targetBranch, string options = "", bool ignoreConflicts = false )
    {
        // Check that the current branch is the target branch.
        if ( !TryGetCurrentBranch( context, out var currentBranch ) )
        {
            return false;
        }

        if ( currentBranch != targetBranch )
        {
            context.Console.WriteError( $"The current branch is '{currentBranch}', but should be '{targetBranch}'." );

            return false;
        }

        var command = "git";
        var arguments = $"merge {sourceBranch} {options}"; 
        
        if ( ignoreConflicts )
        {
            var success = ToolInvocationHelper.InvokeTool(
                context.Console,
                command,
                arguments,
                context.RepoDirectory,
                out _,
                out var output );

            context.Console.WriteMessage( output );

            if ( success )
            {
                return true;
            }
            else if ( output.Split( '\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries )
                         .LastOrDefault()
                         ?.Equals( "Automatic merge failed; fix conflicts and then commit the result.", StringComparison.Ordinal ) ?? false )
            {
                // Git merge always returns the same error code. 
                return true;
            }
            else
            {
                return false;
            }
        }
        else if ( !ToolInvocationHelper.InvokeTool(
                     context.Console,
                     command,
                     arguments,
                     context.RepoDirectory ) )
        {
            return false;
        }

        return true;
    }

    // https://stackoverflow.com/a/48117629/4100001
    public static bool TryResolveUsingOurs( BuildContext context, string file )
    {
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"checkout HEAD -- {file}",
                context.RepoDirectory ) )
        {
            return false;
        }

        return true;
    }

    private static bool TryGetOriginUrl( BuildContext context, BaseBuildSettings settings, [NotNullWhen( true )] out string? url )
    {
        url = null;

        ToolInvocationHelper.InvokeTool(
            context.Console,
            "git",
            $"remote get-url origin",
            context.RepoDirectory,
            out var gitExitCode,
            out var gitOutput );

        if ( gitExitCode != 0 )
        {
            context.Console.WriteError( gitOutput );

            return false;
        }

        url = gitOutput.Trim();

        var isHttps = url.StartsWith( "https", StringComparison.InvariantCulture );

        // When on TeamCity, origin will be updated to form including Git authentication credentials.
        if ( isHttps && TeamCityHelper.IsTeamCityBuild( settings ) )
        {
            if ( !TeamCityHelper.TryGetTeamCitySourceWriteToken(
                    out var teamcitySourceWriteTokenEnvironmentVariableName,
                    out var teamcitySourceCodeWritingToken ) )
            {
                context.Console.WriteImportantMessage(
                    $"{teamcitySourceWriteTokenEnvironmentVariableName} environment variable is not set. Using default credentials." );
            }
            else
            {
                url = url.Insert( 8, $"teamcity%40postsharp.net:{teamcitySourceCodeWritingToken}@" );
            }
        }

        return true;
    }

    public static bool TryPush( BuildContext context, BaseBuildSettings settings )
    {
        if ( !TryGetOriginUrl( context, settings, out var originUrl ) )
        {
            return false;
        }

        // Push completed merge operation to remote.
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"push {originUrl}",
                context.RepoDirectory ) )
        {
            return false;
        }

        return true;
    }

    public static bool TryGetStatus( BuildContext context, string repo, [NotNullWhen( true )] out string[]? status )
    {
        if ( !ToolInvocationHelper.InvokeTool(
                 context.Console,
                 "git",
                 "status --porcelain",
                 repo,
                 out var exitCode,
                 out var statusOutput )
             || exitCode != 0 )
        {
            context.Console.WriteError( statusOutput );
            status = null;

            return false;
        }

        // Environment.NewLine is not correct here.
        status = statusOutput.Split( '\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries );

        return true;
    }

    public static bool CheckNoChange( BuildContext context, CommonCommandSettings settings, string repo )
    {
        if ( !settings.Force )
        {
            if ( !TryGetStatus( context, repo, out var status ) )
            {
                return false;
            }

            if ( status.Length > 0 )
            {
                context.Console.WriteError( $"There are non-committed changes in '{repo}' Use --force." );
                context.Console.WriteImportantMessage( string.Join( Environment.NewLine, status ) );

                return false;
            }
        }

        return true;
    }

    public static bool TryGetRemoteUrl( BuildContext context, [NotNullWhen( true )] out string? url ) => TryGetRemoteUrl( context, "origin", out url );

    public static bool TryGetRemoteUrl( BuildContext context, string remoteName, [NotNullWhen( true )] out string? url )
    {
        if ( !ToolInvocationHelper.InvokeTool(
                 context.Console,
                 "git",
                 $"config --get remote.{remoteName}.url",
                 context.RepoDirectory,
                 out var exitCode,
                 out var output )
             || exitCode != 0 )
        {
            context.Console.WriteError( output );
            url = null;

            return false;
        }

        url = output.Trim();

        return true;
    }
}