using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console.Cli;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Git
{
    internal class GitBulkRenameCommand : Command<GitBulkRenameSettings>
    {
        // ReSharper disable RedundantNullableFlowAttribute
        public override int Execute( [NotNull] CommandContext context, [NotNull] GitBulkRenameSettings settings )
        {
            // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/file-system/how-to-iterate-through-a-directory-tree

            var console = new ConsoleHelper();

            var root = new DirectoryInfo( settings.RepositoryRoot );

            if ( root.GetDirectories( ".git" ).Length == 0 )
            {
                console.WriteError( $"'{settings.RepositoryRoot}' is not a repository root." );

                return 1;
            }

            if ( !RenameAll( console, root, settings.OriginalSubstring, settings.NewSubstring, root.FullName ) )
            {
                return 1;
            }

            return 0;
        }

        private static bool RenameAll( ConsoleHelper console, DirectoryInfo directory, string originalSubstring, string newSubstring, string rootPath )
        {
            if ( directory.Name == ".git" )
            {
                return true;
            }

            var subdirectories = directory.GetDirectories();

            foreach ( var subdirectory in subdirectories )
            {
                if ( !RenameAll( console, subdirectory, originalSubstring, newSubstring, rootPath ) )
                {
                    return false;
                }
            }

            foreach ( var file in directory.GetFiles() )
            {
                if ( !GitRename( console, file, directory, originalSubstring, newSubstring, rootPath ) )
                {
                    return false;
                }
            }

            return GitRename( console, directory, directory.Parent!, originalSubstring, newSubstring, rootPath );
        }

        private static bool GitRename(
            ConsoleHelper console,
            FileSystemInfo renamedNode,
            DirectoryInfo containingDirectory,
            string originalSubstring,
            string newSubstring,
            string rootPath )
        {
            var newName = renamedNode.Name;

            if ( renamedNode.Name.Contains( originalSubstring, StringComparison.Ordinal ) )
            {
                newName = newName.Replace( originalSubstring, newSubstring, StringComparison.Ordinal );

                var pathFrom = renamedNode.FullName;
                var pathTo = Path.Combine( containingDirectory.FullName, newName );

                if ( pathFrom == rootPath )
                {
                    Directory.Move( pathFrom, pathTo );
                }
                else
                {
                    if ( !ToolInvocationHelper.InvokeTool(
                            console,
                            "git",
                            $"mv \"{pathFrom}\" \"{pathTo}\"",
                            rootPath ) )
                    {
                        return false;
                    }
                }
            }

            if ( newName.Contains( originalSubstring, StringComparison.OrdinalIgnoreCase ) )
            {
                console.WriteWarning( $"Other casings found when renaming '{renamedNode.Name}' to '{newName}'." );
            }

            return true;
        }
    }
}