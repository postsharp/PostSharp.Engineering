using Spectre.Console;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

// ReSharper disable AccessToDisposedClosure

namespace PostSharp.Engineering.BuildTools.Utilities
{
    public static class ToolInvocationHelper
    {
        public static bool InvokePowershell(
            ConsoleHelper console,
            string fileName,
            string commandLine,
            string workingDirectory,
            ToolInvocationOptions? options = null )
            => InvokeTool(
                console,
                "powershell",
                $"-NonInteractive -File {fileName} {commandLine}",
                workingDirectory,
                options );

        public static bool InvokeTool(
            ConsoleHelper console,
            string fileName,
            string commandLine,
            string workingDirectory,
            ToolInvocationOptions? options = null )
        {
            if ( !InvokeTool(
                    console,
                    fileName,
                    commandLine,
                    workingDirectory,
                    out var exitCode,
                    options ) )
            {
                return false;
            }
            else if ( exitCode != 0 )
            {
                console.WriteError( "The process \"{0}\" failed with exit code {1}.", fileName, exitCode );

                return false;
            }
            else
            {
                return true;
            }
        }

        public static bool InvokeTool(
            ConsoleHelper console,
            string fileName,
            string commandLine,
            string workingDirectory,
            out int exitCode,
            ToolInvocationOptions? options = null )
            => InvokeTool(
                console,
                fileName,
                commandLine,
                workingDirectory,
                ConsoleHelper.CancellationToken,
                out exitCode,
                s =>
                {
                    if ( !string.IsNullOrWhiteSpace( s ) )
                    {
                        console.WriteMessage( s );
                    }
                },
                s =>
                {
                    if ( !string.IsNullOrWhiteSpace( s ) )
                    {
                        if ( s.Contains( ": warning ", StringComparison.Ordinal ) ||
                             s.Contains( "]Warning:[", StringComparison.Ordinal ) /*docfx*/ )
                        {
                            console.WriteWarning( s );
                        }
                        else if ( s.Contains( ": error ", StringComparison.Ordinal ) ||
                                  s.Contains( "]error:[", StringComparison.Ordinal ) /*docfx*/ )
                        {
                            console.WriteError( s );
                        }
                        else if ( s.StartsWith( "Passed! ", StringComparison.Ordinal ) )
                        {
                            console.WriteSuccess( s );
                        }
                        else if ( s.StartsWith( "Test run for ", StringComparison.Ordinal ) )
                        {
                            console.WriteImportantMessage( s );
                        }
                        else
                        {
                            console.WriteMessage( s );
                        }
                    }
                },
                options );

        // #16205 We don't allow cancellation here because there's no other working way to wait for a process exit
        // than Process.WaitForExit() on .NET Core when capturing process output.
        public static bool InvokeTool(
            ConsoleHelper console,
            string fileName,
            string commandLine,
            string workingDirectory,
            out int exitCode,
            out string output,
            ToolInvocationOptions? options = null )
        {
            StringBuilder stringBuilder = new();

            var success =
                InvokeTool(
                    console,
                    fileName,
                    commandLine,
                    workingDirectory,
                    null,
                    out exitCode,
                    s =>
                    {
                        lock ( stringBuilder )
                        {
                            stringBuilder.Append( s );
                            stringBuilder.Append( '\n' );
                        }
                    },
                    s =>
                    {
                        lock ( stringBuilder )
                        {
                            stringBuilder.Append( s );
                            stringBuilder.Append( '\n' );
                        }
                    },
                    options );

            output = stringBuilder.ToString();

            return success && exitCode == 0;
        }

        private static bool InvokeTool(
            ConsoleHelper console,
            string fileName,
            string commandLine,
            string workingDirectory,
            CancellationToken? cancellationToken,
            out int exitCode,
            Action<string> handleErrorData,
            Action<string> handleOutputData,
            ToolInvocationOptions? options = null )
        {
            exitCode = 0;

#pragma warning disable CA1307 // There is no string.Contains that takes a StringComparison
            if ( fileName.Contains( new string( Path.DirectorySeparatorChar, 1 ) ) && !File.Exists( fileName ) )
            {
                console.WriteError( "Cannot execute \"{0}\": file not found.", fileName );

                return false;
            }
#pragma warning restore CA1307

            const int restartLimit = 3;
            var restartCount = 0;
        start:

            ProcessStartInfo startInfo =
                new()
                {
                    FileName = fileName,
                    Arguments = Environment.ExpandEnvironmentVariables( commandLine ),
                    WorkingDirectory = workingDirectory,
                    ErrorDialog = false,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

            if ( options?.EnvironmentVariables != null )
            {
                foreach ( var pair in options.EnvironmentVariables )
                {
                    startInfo.Environment[pair.Key] = pair.Value;
                }
            }

            Path.GetFileName( fileName );
            Process process = new() { StartInfo = startInfo };

            using ( ManualResetEvent stdErrorClosed = new( false ) )
            using ( ManualResetEvent stdOutClosed = new( false ) )
            {
                process.ErrorDataReceived += ( _, args ) =>
                {
                    try
                    {
                        if ( args.Data == null )
                        {
                            stdErrorClosed.Set();
                        }
                        else
                        {
                            handleErrorData( args.Data );
                        }
                    }
                    catch ( Exception e )
                    {
                        console.Error.WriteException( e );
                    }
                };

                process.OutputDataReceived += ( _, args ) =>
                {
                    try
                    {
                        if ( args.Data == null )
                        {
                            stdOutClosed.Set();
                        }
                        else
                        {
                            handleOutputData( args.Data );
                        }
                    }
                    catch ( Exception e )
                    {
                        console.Error.WriteException( e );
                    }
                };

                // Log the command line, but not the one with expanded environment variables, so we don't expose secrets.
                if ( !(options?.Silent ?? false) )
                {
                    console.WriteImportantMessage( "{0} {1}", process.StartInfo.FileName, commandLine );
                }

                using ( process )
                {
                    try
                    {
                        process.Start();
                    }
                    catch ( Win32Exception e ) when ( (uint) e.NativeErrorCode == 0x80004005 )
                    {
                        if ( restartCount < restartLimit )
                        {
                            console.WriteWarning(
                                "Access denied when starting a process. This might be caused by an anti virus software. Waiting 1000 ms and restarting." );

                            Thread.Sleep( 1000 );
                            restartCount++;

                            goto start;
                        }

                        throw;
                    }

                    if ( !cancellationToken.HasValue )
                    {
                        process.BeginErrorReadLine();
                        process.BeginOutputReadLine();
                        process.WaitForExit();
                    }
                    else
                    {
                        using ( ManualResetEvent cancelledEvent = new( false ) )
                        using ( ManualResetEvent exitedEvent = new( false ) )
                        {
                            process.EnableRaisingEvents = true;
                            process.Exited += ( _, _ ) => exitedEvent.Set();

                            using ( cancellationToken.Value.Register( () => cancelledEvent.Set() ) )
                            {
                                process.BeginErrorReadLine();
                                process.BeginOutputReadLine();

                                if ( !process.HasExited )
                                {
                                    var signal = WaitHandle.WaitAny( new WaitHandle[] { exitedEvent, cancelledEvent } );

                                    if ( signal == 1 )
                                    {
                                        cancellationToken.Value.ThrowIfCancellationRequested();
                                    }
                                }
                            }
                        }
                    }

                    // We will wait for a while for all output to be processed.
                    if ( !cancellationToken.HasValue )
                    {
                        WaitHandle.WaitAll( new WaitHandle[] { stdErrorClosed, stdOutClosed }, 10000 );
                    }
                    else
                    {
                        var i = 0;

                        while ( !WaitHandle.WaitAll( new WaitHandle[] { stdErrorClosed, stdOutClosed }, 100 ) &&
                                i++ < 100 )
                        {
                            cancellationToken.Value.ThrowIfCancellationRequested();
                        }
                    }

                    exitCode = process.ExitCode;

                    return true;
                }
            }
        }
    }
}