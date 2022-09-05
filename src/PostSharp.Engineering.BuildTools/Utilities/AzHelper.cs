﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PostSharp.Engineering.BuildTools.Utilities
{
    public static class AzHelper
    {
        private const string _exe = "cmd";
        private const string _batch = "az.cmd";

        private static string? _cmdArgsFormat;

        private static bool TryFormatCmdArgs( ConsoleHelper console, string args, [MaybeNullWhen( false )] out string cmdArgs )
        {
            if ( _cmdArgsFormat == null )
            {
                var exe = "where";
                var whereArgs = _batch;

                if ( !ToolInvocationHelper.InvokeTool( console, exe, whereArgs, Environment.CurrentDirectory, out var _, out var whereOutput ) )
                {
                    console.WriteError( $"Error executing {exe} {whereArgs}" );
                    console.WriteError( whereOutput );

                    cmdArgs = null;

                    return false;
                }

                _cmdArgsFormat = $"/c \"{whereOutput.Trim()}\" {{0}}";
            }

            cmdArgs = string.Format( CultureInfo.InvariantCulture, _cmdArgsFormat, args );

            return true;
        }

        private static bool Login( ConsoleHelper console, bool dry )
        {
            const string identityUserNameEnvironmentVariableName = "AZ_IDENTITY_USERNAME";
            var identityUserName = Environment.GetEnvironmentVariable( identityUserNameEnvironmentVariableName );

            if ( identityUserName == null )
            {
                console.WriteImportantMessage(
                    $"{identityUserNameEnvironmentVariableName} environment variable not set. If the authorization fails, set this variable to use managed user identity or call 'az login'." );

                return true;
            }
            else
            {
                var azArgs = $"login --identity --username {identityUserName}";

                if ( !TryFormatCmdArgs( console, azArgs, out var cmdArgs ) )
                {
                    return false;
                }

                if ( dry )
                {
                    console.WriteImportantMessage( $"Dry run: {_exe} {cmdArgs}" );

                    return true;
                }
                else
                {
                    return ToolInvocationHelper.InvokeTool(
                        console,
                        _exe,
                        cmdArgs,
                        Environment.CurrentDirectory );
                }
            }
        }

        public static bool Query( ConsoleHelper console, string args, bool dry, [MaybeNullWhen( false )] out string output )
        {
            if ( !Login( console, dry ) )
            {
                output = null;

                return false;
            }

            if ( !TryFormatCmdArgs( console, args, out var cmdArgs ) )
            {
                output = null;

                return false;
            }

            if ( dry )
            {
                console.WriteImportantMessage( $"Dry run: {_exe} {cmdArgs}" );

                output = "<dry>";

                return true;
            }
            else
            {
                if ( !ToolInvocationHelper.InvokeTool( console, _exe, cmdArgs, Environment.CurrentDirectory, out _, out output ) )
                {
                    console.WriteError( output );

                    return false;
                }

                return true;
            }
        }

        public static bool Run( ConsoleHelper console, string args, bool dry )
        {
            if ( !Login( console, dry ) )
            {
                return false;
            }

            if ( !TryFormatCmdArgs( console, args, out var cmdArgs ) )
            {
                return false;
            }

            if ( dry )
            {
                console.WriteImportantMessage( $"Dry run: {_exe} {cmdArgs}" );

                return true;
            }
            else
            {
                return ToolInvocationHelper.InvokeTool( console, _exe, cmdArgs, Environment.CurrentDirectory );
            }
        }
    }
}