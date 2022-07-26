using System;
using System.Diagnostics;
using System.Linq;
using System.Management;

#pragma warning disable CA1416 // Available on Windows only.

namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Kills all processes that may lock build artefacts.
    /// </summary>
    public class KillCommand : BaseCommand<KillCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, KillCommandSettings settings )
        {
            context.Console.WriteHeading( "Killing compiler processes" );

            var currentSessionId = Process.GetCurrentProcess().SessionId;

            var processesToKill = Process.GetProcesses()
                .Where( p => p.SessionId == currentSessionId )
                .Where(
                    p =>
                    {
                        if ( p.ProcessName.Equals( "VBCSCompiler", StringComparison.OrdinalIgnoreCase )
                             || p.ProcessName.Equals( "MSBuild", StringComparison.OrdinalIgnoreCase ) )
                        {
                            return true;
                        }

                        if ( p.ProcessName.Equals( "dotnet", StringComparison.OrdinalIgnoreCase ) )
                        {
                            var commandLine = GetCommandLine( p );

                            if ( commandLine != null
                                 && (commandLine.Contains( "VBCSCompiler", StringComparison.OrdinalIgnoreCase )
                                     || commandLine.Contains( "MSBuild", StringComparison.OrdinalIgnoreCase )
                                     || commandLine.Contains( "tool", StringComparison.OrdinalIgnoreCase )) )
                            {
                                return true;
                            }
                        }

                        if ( p.ProcessName.Equals( "testhost", StringComparison.OrdinalIgnoreCase ) 
                             || p.ProcessName.Equals( "testhost.x86", StringComparison.OrdinalIgnoreCase ) )
                        {
                            return true;
                        }

                        return false;
                    } )
                .ToList();

            if ( processesToKill.Count == 0 )
            {
                context.Console.WriteImportantMessage( "No process found." );
            }
            else
            {
                foreach ( var process in processesToKill )
                {
                    context.Console.WriteMessage( $"Killing process {process.Id}: {GetCommandLine( process )}" );

                    if ( !settings.Dry )
                    {
                        try
                        {
                            process.Kill( true );
                        }
                        catch ( Exception e )
                        {
                            context.Console.WriteWarning( e.Message );
                        }
                    }
                }
            }

            return true;
        }

        private static string? GetCommandLine( Process process )
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new( "SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id );

                using ( var objects = searcher.Get() )
                {
                    return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}