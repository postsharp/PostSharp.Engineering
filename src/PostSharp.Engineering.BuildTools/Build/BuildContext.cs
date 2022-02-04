using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console.Cli;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build
{
    public class BuildContext
    {
        public ConsoleHelper Console { get; }

        public string RepoDirectory { get; }

        public Product Product { get; }

        public string Branch { get; }

        public string GetManifestFilePath( BuildConfiguration configuration )
        {
            return Path.Combine(
                this.RepoDirectory,
                this.Product.PrivateArtifactsDirectory.ToString(
                    new VersionInfo( null!, configuration.ToString(), this.Product.Configurations[configuration].MSBuildName ) ),
                $"{this.Product.ProductName}.version.props" );
        }

        private BuildContext( ConsoleHelper console, string repoDirectory, Product product, string branch )
        {
            this.Console = console;
            this.RepoDirectory = repoDirectory;
            this.Product = product;
            this.Branch = branch;
        }

        public static bool TryCreate(
            CommandContext commandContext,
            [NotNullWhen( true )] out BuildContext? buildContext )
        {
            var repoDirectory = FindRepoDirectory( Environment.CurrentDirectory );
            var console = new ConsoleHelper();

            if ( repoDirectory == null )
            {
                console.WriteError( "This tool must be called from a git repository." );
                buildContext = null;

                return false;
            }

            if ( !ToolInvocationHelper.InvokeTool( console, "git", "rev-parse --abbrev-ref HEAD", repoDirectory, out var gitExitCode, out var gitOutput )
                 || gitExitCode != 0 )
            {
                buildContext = null;

                return false;
            }

            buildContext = new BuildContext( console, repoDirectory, (Product) commandContext.Data!, gitOutput.Trim() );

            return true;
        }

        private static string? FindRepoDirectory( string directory )
        {
            if ( Directory.Exists( Path.Combine( directory, ".git" ) ) )
            {
                return directory;
            }
            else
            {
                var parentDirectory = Path.GetDirectoryName( directory );

                if ( parentDirectory != null )
                {
                    return FindRepoDirectory( parentDirectory );
                }
                else
                {
                    return null;
                }
            }
        }
    }
}