// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;

#pragma warning disable CA1416 // Available on Windows only.

namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Kills all processes that may lock build artefacts.
    /// </summary>
    [UsedImplicitly]
    public class KillCommand : BaseCommand<KillCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, KillCommandSettings settings )
        {
            context.Console.WriteHeading( "Killing processes" );

            var currentSessionId = Process.GetCurrentProcess().SessionId;

            var processesToKill = Process.GetProcesses()
                .Where( p => p.SessionId == currentSessionId )
                .Where(
                    p =>
                    {
                        if ( p.ProcessName.StartsWith( "redis", StringComparison.OrdinalIgnoreCase ) )
                        {
                            return true;
                        }

                        if ( p.ProcessName.Equals( "VBCSCompiler", StringComparison.OrdinalIgnoreCase )
                             || p.ProcessName.Equals( "MSBuild", StringComparison.OrdinalIgnoreCase )
                             || p.ProcessName.Contains( "PostSharp", StringComparison.OrdinalIgnoreCase ) )
                        {
                            return true;
                        }

                        if ( p.ProcessName.Equals( "dotnet", StringComparison.OrdinalIgnoreCase ) &&
                             ReferencesAny( context, p, new[] { "Metalama", "VBCSCompiler", "MSBuild" } ) )
                        {
                            return true;
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
                    context.Console.WriteMessage( $"Killing process {process.Id} ({process.ProcessName}): {GetCommandLine( process )}" );

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
                    new( $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}" );

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

        private static bool ReferencesAny( BuildContext context, Process process, string[] substrings )
        {
            try
            {
                foreach ( ProcessModule module in process.Modules )
                {
                    if ( module.FileName != null! )
                    {
                        if ( substrings.Any( s => Path.GetFileNameWithoutExtension( module.FileName ).Contains( s, StringComparison.OrdinalIgnoreCase ) ) )
                        {
                            return true;
                        }
                    }
                }

                return true;
            }
            catch ( Exception e )
            {
                if ( !process.HasExited )
                {
                    context.Console.WriteWarning( $"Cannot enumerate the modules of '{process.Id}': {e.Message}." );
                }

                return false;
            }
        }
    }
}