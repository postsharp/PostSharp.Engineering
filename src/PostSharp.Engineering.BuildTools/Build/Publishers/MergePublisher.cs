using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Build.Publishers;

public class MergePublisher : Publisher
{
    public MergePublisher( Pattern files ) : base( files ) { }

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
        // Change to the master branch before we do merge.
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"checkout master",
                context.RepoDirectory ) )
        {
            return false;
        }

        // Pull changes because local master may not contain all changes.
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"pull",
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

    private static bool UpdateDependenciesVersions( BuildContext context, PublishSettings settings )
    {
        // TODO: I will probably need a Prepare() step here, because there might be a new build from the BuildServer
        // TODO: that agent will download during Build [Public], but other agent may not have that build downloaded.

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
                context.Console.WriteMessage( $"Skipping version update of dependency local/feed '{dependency.Name}'." );

                continue;
            }

            var ciBuildId = dependencySource.BuildServerSource as CiBuildId;

            // If there is no CI build specification for the dependency, there is a problem.
            if ( ciBuildId == null )
            {
                return false;
            }

            // Path to the downloaded build version file.
            var dependencyVersionFile = Path.Combine(
                Environment.GetEnvironmentVariable( "USERPROFILE" ) ?? Path.GetTempPath(),
                ".build-artifacts",
                dependency.Name,
                dependency.CiBuildTypes[BuildConfiguration.Public],
                ciBuildId.BuildNumber.ToString( CultureInfo.InvariantCulture ),
                context.Product.PrivateArtifactsDirectory.ToString(),
                $"{dependency.Name}.version.props" );

            var productVersionsPropertiesFile = Path.Combine( context.RepoDirectory, context.Product.VersionsFilePath );

            // Load the up-to-date version file of dependency.
            var currentVersionDocument = XDocument.Load( dependencyVersionFile );
            var project = currentVersionDocument.Root;
            var props = project!.Element( "PropertyGroup" );
            var currentDependencyVersionValue = props!.Element( $"{dependency.NameWithoutDot}Version" )!.Value;

            // Load current product Versions.props.
            var versionDocumentToUpdate = XDocument.Load( productVersionsPropertiesFile, LoadOptions.PreserveWhitespace );
            project = versionDocumentToUpdate.Root;
            props = project!.Elements( "PropertyGroup" ).SingleOrDefault( p => p.Element( $"{dependency.NameWithoutDot}Version" ) != null );
            var oldVersionElement = props!.Elements( $"{dependency.NameWithoutDot}Version" ).SingleOrDefault( p => !p.HasAttributes );
            var oldVersionValue = oldVersionElement!.Value;

            var currentDependencyVersionNumber =
                Version.Parse( currentDependencyVersionValue.Substring( 0, currentDependencyVersionValue.IndexOf( '-', StringComparison.InvariantCulture ) ) );

            var oldDependencyVersionNumber = Version.Parse( oldVersionValue.Substring( 0, oldVersionValue.IndexOf( '-', StringComparison.InvariantCulture ) ) );

            // No need to rewrite the file if there is no change in version.
            if ( currentDependencyVersionNumber == oldDependencyVersionNumber )
            {
                context.Console.WriteMessage( $"Version of '{dependency}' dependency is up to date." );

                return true;
            }

            oldVersionElement.Value = currentDependencyVersionValue;

            var xmlWriterSettings =
                new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, IndentChars = "    ", Encoding = new UTF8Encoding( false ) };

            using ( var xmlWriter = XmlWriter.Create( productVersionsPropertiesFile, xmlWriterSettings ) )
            {
                versionDocumentToUpdate.Save( xmlWriter );
            }

            context.Console.WriteMessage( $"Bumping version dependency '{dependency}' from '{oldVersionValue}' to '{currentDependencyVersionValue}'." );
        }

        return true;
    }

    private static bool CommitDependenciesVersionsBumped( BuildContext context )
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
                $"remote get-url origin",
                context.RepoDirectory,
                out _,
                out var gitOrigin ) )
        {
            return false;
        }

        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"commit -m \"Dependencies versions updated.\"",
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

    public override SuccessCode Execute(
        BuildContext context,
        PublishSettings settings,
        string file,
        BuildInfo buildInfo,
        BuildConfigurationInfo configuration )
    {
        context.Console.WriteMessage( "Merging dev branch to master." );

        // If Product doesn't require merging changes into master branch, we skip merging.
        if ( context.Product.RequiresBranchMerging )
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

            // Defines if we need to do a merge. If the commit hashes are equal, there haven't been any unmerged commits, or the current branch is actually master.
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
            if ( !UpdateDependenciesVersions( context, settings ) )
            {
                return SuccessCode.Error;
            }

            // Commit changes made to Versions.props.
            if ( !CommitDependenciesVersionsBumped( context ) )
            {
                return SuccessCode.Error;
            }
        }

        return SuccessCode.Success;
    }
}