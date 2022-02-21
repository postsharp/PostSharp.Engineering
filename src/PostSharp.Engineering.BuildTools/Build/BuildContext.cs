using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console.Cli;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Exposes all the current build context, i.e. anything that depends on the environment or the current product, but not depending on
    /// the command line.
    /// </summary>
    public class BuildContext
    {
        /// <summary>
        /// Gets an object that allows to write messages to the console.
        /// </summary>
        public ConsoleHelper Console { get; }

        /// <summary>
        /// Gets the root directory of the git repository.
        /// </summary>
        public string RepoDirectory { get; }

        /// <summary>
        /// Gets the current <see cref="PostSharp.Engineering.BuildTools.Build.Model.Product"/> definition.
        /// </summary>
        public Product Product { get; }

        /// <summary>
        /// Gets the name of the current git branch.
        /// </summary>
        public string Branch { get; }

        /// <summary>
        /// Gets the full path of the current manifest file (i.e. the file called <c>My.Product.version.props</c>)
        /// for a given <see cref="BuildConfiguration"/>.
        /// </summary>
        public string GetManifestFilePath( BuildConfiguration configuration )
        {
            return Path.Combine(
                this.RepoDirectory,
                this.Product.PrivateArtifactsDirectory.ToString(
                    new BuildInfo( null!, configuration.ToString(), this.Product.Configurations[configuration].MSBuildName ) ),
                $"{this.Product.ProductName}.version.props" );
        }

        private BuildContext( ConsoleHelper console, string repoDirectory, Product product, string branch )
        {
            this.Console = console;
            this.RepoDirectory = repoDirectory;
            this.Product = product;
            this.Branch = branch;
        }

        /// <summary>
        /// Tries to create a <see cref="BuildContext"/> from a <see cref="CommandContext"/>.
        /// </summary>
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