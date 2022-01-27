using System;
using System.Diagnostics.CodeAnalysis;

namespace PostSharp.Engineering.BuildTools.Utilities
{
    public static class AzHelper
    {
        private const string _exe = "exe";

        private static bool Login( ConsoleHelper console, bool dry )
        {
            const string identityUserNameEnvironmentVariableName = "AZ_IDENTITY_USERNAME";
            var identityUserName = Environment.GetEnvironmentVariable( identityUserNameEnvironmentVariableName );

            if ( identityUserName == null )
            {
                console.WriteImportantMessage( $"{identityUserNameEnvironmentVariableName} environment variable not set. If the authorization fails, set this variable to use managed user identity or call 'az login'." );

                return true;
            }
            else
            {
                var args = $"login --identity --username {identityUserName}";

                if ( dry )
                {
                    console.WriteImportantMessage( $"Dry run: {_exe} {args}" );

                    return true;
                }
                else
                {
                    return ToolInvocationHelper.InvokeTool(
                            console,
                            _exe,
                            args,
                            Environment.CurrentDirectory );
                }
            }
        }

        public static bool Query( ConsoleHelper console, string args, bool dry, [MaybeNullWhen(false)] out string output )
        {
            if ( !Login( console, dry ) )
            {
                output = null;
                return false;
            }

            if ( dry )
            {
                console.WriteImportantMessage( $"Dry run: {_exe} {args}" );

                output = "<dry>";
                return true;
            }
            else
            {
                return ToolInvocationHelper.InvokeTool( console, _exe, args, Environment.CurrentDirectory, out _, out output );
            }
        }

        public static bool Run( ConsoleHelper console, string args, bool dry )
        {
            if ( !Login( console, dry ) )
            {
                return false;
            }

            if ( dry )
            {
                console.WriteImportantMessage( $"Dry run: {_exe} {args}" );

                return true;
            }
            else
            {
                return ToolInvocationHelper.InvokeTool( console, _exe, args, Environment.CurrentDirectory );
            }
        }
    }
}