// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

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
        /// Gets the name of the current git branch. Can be for instance a topic branch.
        /// </summary>
        public string Branch { get; }

        public CommandContext CommandContext { get; }

        /// <summary>
        /// Gets a value indicating whether the current device is a guest device, as opposed to a device owned and configured by PostSharp.
        /// The main difference is that guest devices use feed packages while company devices use TeamCity artefacts.
        /// </summary>
        public static bool IsGuestDevice => !bool.TryParse( Environment.GetEnvironmentVariable( "IS_POSTSHARP_OWNED" ), out var value ) || !value;

        /// <summary>
        /// Gets the full path of the current manifest file (i.e. the file called <c>My.Product.version.props</c>)
        /// for a given <see cref="BuildConfiguration"/>.
        /// </summary>
        public string GetManifestFilePath( BuildConfiguration configuration )
        {
            return Path.Combine(
                this.RepoDirectory,
                this.Product.PrivateArtifactsDirectory.ToString(
                    new BuildInfo( null, configuration.ToString(), this.Product.DependencyDefinition.MSBuildConfiguration[configuration], null ) ),
                $"{this.Product.ProductName}.version.props" );
        }

        private BuildContext( ConsoleHelper console, string repoDirectory, Product product, string branch, CommandContext commandContext )
        {
            this.Console = console;
            this.RepoDirectory = repoDirectory;
            this.Product = product;
            this.Branch = branch;
            this.CommandContext = commandContext;
        }

        /// <summary>
        /// Tries to create a <see cref="BuildContext"/> from a <see cref="Spectre.Console.Cli.CommandContext"/>.
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

            if ( !GitHelper.TryGetCurrentBranch( console, repoDirectory, out var currentBranch ) )
            {
                buildContext = null;

                return false;
            }

            buildContext = new BuildContext( console, repoDirectory, (Product) commandContext.Data!, currentBranch, commandContext );

            return true;
        }

        private static string? FindRepoDirectory( string directory )
        {
            if ( Directory.Exists( Path.Combine( directory, ".git" ) ) )
            {
                var globalJson = Path.Combine( directory, "global.json" );

                if ( !File.Exists( globalJson ) )
                {
                    throw new FileNotFoundException( $"The file '{globalJson}' must exist, even empty." );
                }

                // Resolve links and junctions.
                var realPath = FileSystemHelper.GetFinalPath( globalJson );

                return Path.GetDirectoryName( realPath );
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

        public BuildContext WithConsoleHelper( ConsoleHelper consoleHelper )
            => new( consoleHelper, this.RepoDirectory, this.Product, this.Branch, this.CommandContext );
    }
}