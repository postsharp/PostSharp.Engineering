// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;

namespace PostSharp.Engineering.BuildTools.Utilities
{
    public class DotNetTool
    {
        public string PackageId { get; }

        public string Command { get; }

        public string Version { get; }

        public string Alias { get; }

        public static DotNetTool SignClient { get; } = new SignTool();

        public static DotNetTool Resharper { get; } = new( "jb", "JetBrains.Resharper.GlobalTools", "2022.3.0", "jb" );

        public static ImmutableArray<DotNetTool> All { get; } = ImmutableArray.Create( SignClient, Resharper );

        public DotNetTool( string alias, string packageId, string version, string command )
        {
            this.Alias = alias;
            this.PackageId = packageId;
            this.Version = version;
            this.Command = command;
        }

        public virtual bool Invoke( BuildContext context, string command )
        {
            var toolsDirectory = Path.Combine( context.RepoDirectory, context.Product.EngineeringDirectory, "tools" );

            if ( !Directory.Exists( toolsDirectory ) )
            {
                Directory.CreateDirectory( toolsDirectory );
            }

            // 1. Create the dotnet tool manifest.
            if ( !Directory.Exists( Path.Combine( toolsDirectory, ".config" ) ) )
            {
                if ( !ToolInvocationHelper.InvokeTool(
                        context.Console,
                        "dotnet",
                        $"new tool-manifest",
                        toolsDirectory ) )
                {
                    return false;
                }
            }

            // Open the config file and see if we have to install or update.
            string? installVerb = null;
            var configFilePath = Path.Combine( toolsDirectory, ".config", "dotnet-tools.json" );
            var configDocument = JsonDocument.Parse( File.ReadAllText( configFilePath ) );

            var installedVersionString = configDocument.RootElement.GetPropertyOrNull( "tools" )
                .GetPropertyOrNull( this.PackageId.ToLowerInvariant() )
                .GetPropertyOrNull( "version" )
                ?.GetString();

            if ( installedVersionString == null )
            {
                installVerb = "install";
            }
            else
            {
                var installedVersion = System.Version.Parse( installedVersionString );

                if ( installedVersion < System.Version.Parse( this.Version ) )
                {
                    installVerb = "update";
                }
            }

            // 2. Restore the tool.
            if ( installVerb != null )
            {
                if ( !ToolInvocationHelper.InvokeTool(
                        context.Console,
                        "dotnet",
                        $"tool {installVerb} {this.PackageId} --version {this.Version} --local --add-source \"https://api.nuget.org/v3/index.json\"",
                        toolsDirectory ) )
                {
                    return false;
                }
            }

            // 3. Restore resource tools.
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

            // 4. Invoke the tool.
            return ToolInvocationHelper.InvokeTool(
                context.Console,
                "dotnet",
                $"tool run {this.Command} {command}",
                toolsDirectory );
        }
    }
}