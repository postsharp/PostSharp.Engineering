using PostSharp.Engineering.BuildTools.Build;
using System;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Utilities
{
    public class DotNetTool
    {
        public string PackageId { get; }

        public string Command { get; }

        public string Version { get; }

        public static DotNetTool SignClient { get; } = new( "SignClient", "1.3.155", "SignClient" );

        public static DotNetTool Resharper { get; } = new( "JetBrains.Resharper.GlobalTools", "2021.3.0", "jb" );

        public DotNetTool( string packageId, string version, string command )
        {
            this.PackageId = packageId;
            this.Version = version;
            this.Command = command;
        }

        public bool Invoke( BuildContext context, string command, string? directory = null )
        {
            var toolsDirectory = Path.Combine( context.RepoDirectory, context.Product.EngineeringDirectory, "tools" );
            var thisToolDirectory = Path.Combine( toolsDirectory, $".store\\{this.PackageId}" );
            var thisToolVersionDirectory = Path.Combine( thisToolDirectory, this.Version );

            // 1. Restore the tool.

            if ( !Directory.Exists( thisToolVersionDirectory ) )
            {
                if ( !Directory.Exists( toolsDirectory ) )
                {
                    Directory.CreateDirectory( toolsDirectory );
                }

                var verb = Directory.Exists( thisToolDirectory ) ? "update" : "install";

                if ( !ToolInvocationHelper.InvokeTool(
                        context.Console,
                        "dotnet",
                        $"tool {verb} {this.PackageId} --version {this.Version} --tool-path \"{toolsDirectory}\" --add-source \"https://nuget.postsharp.net/nuget/Metalama/\" --add-source \"https://api.nuget.org/v3/index.json\"",
                        directory ?? context.RepoDirectory ) )
                {
                    return false;
                }
            }

            // 2. Restore resource tools.
            var assembly = this.GetType().Assembly;

            foreach ( var resourceName in assembly.GetManifestResourceNames() )
            {
                const string prefix = "PostSharp.Engineering.BuildTools.ToolsResources.";

                if ( resourceName.StartsWith( prefix, StringComparison.Ordinal ) )
                {
                    using var resource = assembly.GetManifestResourceStream( resourceName );

                    var file = Path.Combine( toolsDirectory, resourceName.Substring( prefix.Length ) );

                    using ( var outputStream = File.Create( file ) )
                    {
                        resource!.CopyTo( outputStream );
                    }
                }
            }

            command = command.Replace( "$(ToolsDirectory)", toolsDirectory, StringComparison.Ordinal );

            // 3. Invoke the tool.

            var toolPath = Path.Combine( toolsDirectory, $"{this.Command}.exe" );

            return ToolInvocationHelper.InvokeTool(
                context.Console,
                toolPath,
                command,
                context.RepoDirectory );
        }
    }
}