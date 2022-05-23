using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Build.Publishers;

public class MergePublisher : IndependentPublisher
{
    public override SuccessCode Execute(
        BuildContext context,
        PublishSettings settings,
        BuildInfo buildInfo,
        BuildConfigurationInfo configuration )
    {
        // Attempts to get the latest intersection of master and currentBranch in form of each branches' commit hash.
        if ( !TryGetLatestBranchesIntersectionCommitHashes(
                context,
                out var currentBranch,
                out var lastCurrentBranchCommitHash,
                out var lastCommonCommitHash ) )
        {
            return SuccessCode.Error;
        }
        
        context.Console.WriteMessage( $"Merging branch '{currentBranch}' to 'master' after publishing artifacts." );

        // Change to the master branch before we do merge.
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"checkout master",
                context.RepoDirectory ) )
        {
            return SuccessCode.Error;
        }

        // Determines if we need to do a merge: If the commit hashes are equal, there haven't been any unmerged commits, or the current branch is actually master.
        if ( !lastCurrentBranchCommitHash.Equals( lastCommonCommitHash, StringComparison.Ordinal ) )
        {
            context.Console.WriteWarning( $"Branch '{currentBranch}' requires merging to master." );
        
            // Merge current branch.
            if ( !MergeBranchToMaster( context, settings, currentBranch ) )
            {
                return SuccessCode.Error;
            }
        }

        // Go through all dependencies and update their fixed version in Versions.props file.
        if ( !UpdateDependenciesVersions( context, settings, out var dependenciesUpdated ) )
        {
            return SuccessCode.Error;
        }

        // If we updated any dependencies, commit the changes.
        if ( dependenciesUpdated )
        {
            // Commit changes made to Versions.props.
            if ( !TryCommitDependenciesVersionsBumped( context ) )
            {
                return SuccessCode.Error;
            }
        }

        return SuccessCode.Success;
    }

    private static bool TryGetLatestBranchesIntersectionCommitHashes(
        BuildContext context,
        [NotNullWhen( true )] out string? currentBranch,
        [NotNullWhen( true )] out string? lastCurrentBranchCommitHash,
        [NotNullWhen( true )] out string? lastCommonCommitHash )
    {
        lastCurrentBranchCommitHash = null;
        lastCommonCommitHash = null;

        // Fetch all remotes to make sure the merge has not already been done.
        ToolInvocationHelper.InvokeTool(
            context.Console,
            "git",
            $"fetch --all",
            context.RepoDirectory );

        // Returns the reference name of the current branch.
        ToolInvocationHelper.InvokeTool(
            context.Console,
            "git",
            $"branch --show-current",
            context.RepoDirectory,
            out var gitExitCode,
            out var gitOutput );

        if ( gitExitCode != 0 )
        {
            context.Console.WriteError( gitOutput );
            currentBranch = null;

            return false;
        }

        currentBranch = gitOutput.Trim();

        // Returns the last commit on the current branch in the commit hash format.
        ToolInvocationHelper.InvokeTool(
            context.Console,
            "git",
            $"log -n 1 --pretty=format:\"%H\"",
            context.RepoDirectory,
            out gitExitCode,
            out gitOutput );

        if ( gitExitCode != 0 )
        {
            context.Console.WriteError( gitOutput );

            return false;
        }

        lastCurrentBranchCommitHash = gitOutput;

        // Returns hash of as good common ancestor commit as possible between origin/master and current branch.
        // We use origin/master, because master may not be present as a local branch.
        ToolInvocationHelper.InvokeTool(
            context.Console,
            "git",
            $"merge-base origin/master {currentBranch}",
            context.RepoDirectory,
            out gitExitCode,
            out gitOutput );

        if ( gitExitCode != 0 )
        {
            context.Console.WriteError( gitOutput );

            return false;
        }

        lastCommonCommitHash = gitOutput;

        return true;
    }

    private static bool MergeBranchToMaster( BuildContext context, BaseBuildSettings settings, string branchToMerge )
    {
        // Pull remote master changes because local master may not contain all changes or upstream may not be set for local master.
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"pull origin master",
                context.RepoDirectory ) )
        {
            return false;
        }

        // Attempts merging branch to master.
        ToolInvocationHelper.InvokeTool(
            context.Console,
            "git",
            $"merge {branchToMerge}",
            context.RepoDirectory,
            out var gitExitCode,
            out var gitOutput );

        if ( gitExitCode != 0 )
        {
            context.Console.WriteError( gitOutput );

            return false;
        }

        // Returns the remote origin.
        ToolInvocationHelper.InvokeTool(
            context.Console,
            "git",
            $"remote get-url origin",
            context.RepoDirectory,
            out gitExitCode,
            out gitOutput );

        if ( gitExitCode != 0 )
        {
            context.Console.WriteError( gitOutput );

            return false;
        }

        var gitOrigin = gitOutput.Trim();

        var isHttps = gitOrigin.StartsWith( "https", StringComparison.InvariantCulture );

        // When on TeamCity, origin will be updated to form including Git authentication credentials.
        if ( TeamCityHelper.IsTeamCityBuild( settings ) )
        {
            if ( isHttps )
            {
                if ( !TeamCityHelper.TryGetTeamCitySourceWriteToken(
                        out var teamcitySourceWriteTokenEnvironmentVariableName,
                        out var teamcitySourceCodeWritingToken ) )
                {
                    context.Console.WriteImportantMessage(
                        $"{teamcitySourceWriteTokenEnvironmentVariableName} environment variable is not set. Using default credentials." );
                }
                else
                {
                    gitOrigin = gitOrigin.Insert( 8, $"teamcity%40postsharp.net:{teamcitySourceCodeWritingToken}@" );
                }
            }
        }

        // Push completed merge operation to remote.
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"push {gitOrigin}",
                context.RepoDirectory ) )
        {
            return false;
        }

        context.Console.WriteSuccess( $"Merging '{branchToMerge}' into 'master' branch was successful." );

        return true;
    }

    private static bool UpdateDependenciesVersions( BuildContext context, PublishSettings settings, out bool dependenciesUpdated )
    {
        dependenciesUpdated = false;
        var productVersionsPropertiesFile = Path.Combine( context.RepoDirectory, context.Product.VersionsFilePath );

        // For following Prepare step we need to BuildSettings
        var buildSettings = new BuildSettings() { BuildConfiguration = settings.BuildConfiguration, ContinuousIntegration = settings.ContinuousIntegration, Force = settings.Force };

        // Do prepare step to get Version.Public.g.props to load up-to-date versions from.
        if ( !context.Product.PrepareVersionsFile( context, buildSettings, out _ ) )
        {
            return false;
        }

        // Get dependenciesOverrideFile from Versions.Public.g.props.
        if ( !DependenciesOverrideFile.TryLoad( context, settings.BuildConfiguration, out var dependenciesOverrideFile ) )
        {
            return false;
        }

        foreach ( var dependency in context.Product.Dependencies )
        {
            var dependencySource = dependenciesOverrideFile.Dependencies[dependency.Name];

            // We don't automatically change version of Feed or Local dependencies.
            if ( dependencySource.SourceKind == DependencySourceKind.Feed || dependencySource.SourceKind == DependencySourceKind.Local )
            {
                context.Console.WriteMessage( $"Skipping version update of local/feed dependency '{dependency.Name}'." );

                continue;
            }

            // Path to the downloaded build version file.
            var dependencyVersionFile = dependencySource.VersionFile;

            if ( dependencyVersionFile == null )
            {
                return false;
            }

            // Load the up-to-date version file of dependency.
            var upToDateVersionDocument = XDocument.Load( dependencyVersionFile );
            var project = upToDateVersionDocument.Root;
            var props = project!.Element( "PropertyGroup" );
            var currentDependencyVersionValue = props!.Element( $"{dependency.NameWithoutDot}Version" )!.Value;

            // Load current product Versions.props.
            var currentVersionDocument = XDocument.Load( productVersionsPropertiesFile, LoadOptions.PreserveWhitespace );
            project = currentVersionDocument.Root;
            props = project!.Elements( "PropertyGroup" ).SingleOrDefault( p => p.Element( $"{dependency.NameWithoutDot}Version" ) != null );
            var oldVersionElement = props!.Elements( $"{dependency.NameWithoutDot}Version" ).SingleOrDefault( p => !p.HasAttributes );
            var oldVersionValue = oldVersionElement!.Value;

            var currentDependencyVersionNumber =
                Version.Parse( currentDependencyVersionValue.Substring( 0, currentDependencyVersionValue.IndexOf( '-', StringComparison.InvariantCulture ) ) );

            var oldDependencyVersionNumber = Version.Parse( oldVersionValue.Substring( 0, oldVersionValue.IndexOf( '-', StringComparison.InvariantCulture ) ) );

            // We don't need to rewrite the file if there is no change in version.
            if ( currentDependencyVersionNumber == oldDependencyVersionNumber )
            {
                context.Console.WriteMessage( $"Version of '{dependency}' dependency is up to date." );

                continue;
            }

            oldVersionElement.Value = currentDependencyVersionValue;
            dependenciesUpdated = true;

            var xmlWriterSettings =
                new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, IndentChars = "    ", Encoding = new UTF8Encoding( false ) };

            using ( var xmlWriter = XmlWriter.Create( productVersionsPropertiesFile, xmlWriterSettings ) )
            {
                currentVersionDocument.Save( xmlWriter );
            }

            context.Console.WriteMessage( $"Bumping version dependency '{dependency}' from '{oldVersionValue}' to '{currentDependencyVersionValue}'." );
        }

        return true;
    }

    private static bool TryCommitDependenciesVersionsBumped( BuildContext context )
    {
        // Adds Versions.props with updated dependencies versions to Git staging area.
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"add {context.Product.VersionsFilePath}",
                context.RepoDirectory ) )
        {
            return false;
        }

        // Returns the remote origin.
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                "remote get-url origin",
                context.RepoDirectory,
                out _,
                out var gitOrigin ) )
        {
            return false;
        }

        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                "commit -m \"Versions of dependencies updated.\"",
                context.RepoDirectory ) )
        {
            return false;
        }

        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"push {gitOrigin.Trim()}",
                context.RepoDirectory ) )
        {
            return false;
        }

        return true;
    }
}