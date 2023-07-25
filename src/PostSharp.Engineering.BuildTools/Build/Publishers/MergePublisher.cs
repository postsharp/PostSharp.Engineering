﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
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
        // When on TeamCity, Git user credentials are set to TeamCity.
        if ( TeamCityHelper.IsTeamCityBuild( settings ) )
        {
            if ( !TeamCityHelper.TrySetGitIdentityCredentials( context ) )
            {
                return SuccessCode.Error;
            }
        }

        var sourceBranch = context.Product.DependencyDefinition.Branch;
        
        if ( context.Branch != sourceBranch )
        {
            context.Console.WriteError(
                $"{nameof(MergePublisher)} can only be executed on the default branch ('{sourceBranch}'). The current branch is '{context.Branch}'." );

            return SuccessCode.Error;
        }
        
        var targetBranch = context.Product.DependencyDefinition.ReleaseBranch;

        if ( targetBranch == null )
        {
            context.Console.WriteWarning( $"Source code not published. The release branch is not set for '{context.Product.ProductName}' product." );

            return SuccessCode.Success;
        }

        // Go through all dependencies and update their fixed version in Versions.props file.
        if ( !TryParseAndVerifyDependencies( context, settings, out var dependenciesUpdated ) )
        {
            return SuccessCode.Error;
        }

        // We commit and push if dependencies versions were updated in previous step.
        if ( dependenciesUpdated )
        {
            // Commit and push changes made to Versions.props.
            if ( !TryCommitAndPushBumpedDependenciesVersions( context ) )
            {
                return SuccessCode.Error;
            }
        }

        context.Console.WriteHeading( $"Merging branch '{sourceBranch}' to '{targetBranch}' after publishing artifacts." );

        // Checkout to target branch branch and pull to update the local repository.
        if ( !GitHelper.TryCheckoutAndPull( context, targetBranch ) )
        {
            return SuccessCode.Error;
        }

        // Attempts merging from the source branch, forcing conflicting hunks to be auto-resolved in favour of the branch being merged.
        if ( !GitHelper.TryMerge( context, sourceBranch, targetBranch, "--strategy-option theirs" ) )
        {
            return SuccessCode.Error;
        }

        // Push the target branch.
        if ( !GitHelper.TryPush( context, settings ) )
        {
            return SuccessCode.Error;
        }

        context.Console.WriteSuccess( $"Merging '{sourceBranch}' branch into '{targetBranch}' branch was successful." );

        return SuccessCode.Success;
    }

    private static bool TryParseAndVerifyDependencies( BuildContext context, PublishSettings settings, out bool dependenciesUpdated )
    {
        context.Console.WriteImportantMessage( $"Checking versions of auto-updated dependencies." );
        
        dependenciesUpdated = false;

        // For following Prepare step we need to BuildSettings
        var buildSettings = new BuildSettings()
        {
            BuildConfiguration = settings.BuildConfiguration, SimulateContinuousIntegration = settings.SimulateContinuousIntegration, Force = settings.Force
        };

        // Do prepare step to get Version.Public.g.props to load up-to-date versions from.
        if ( !context.Product.PrepareVersionsFile( context, buildSettings, out _ ) )
        {
            return false;
        }

        // Get dependenciesOverrideFile from Versions.Public.g.props.
        if ( !DependenciesOverrideFile.TryLoad( context, settings, settings.BuildConfiguration, out var dependenciesOverrideFile ) )
        {
            return false;
        }

        var autoUpdatedDependencies = dependenciesOverrideFile.Dependencies.Where( d => d.Value.SourceKind != DependencySourceKind.Feed ).ToArray();

        if ( autoUpdatedDependencies.Length == 0 )
        {
            context.Console.WriteMessage( "There are no auto-updated dependencies to check." );
            
            return true;
        }
        
        var autoUpdatedVersionsFilePath = Path.Combine( context.RepoDirectory, context.Product.AutoUpdatedVersionsFilePath );
        var autoUpdatedVersionsFileName = Path.GetFileName( autoUpdatedVersionsFilePath );
        var currentVersionDocument = XDocument.Load( autoUpdatedVersionsFilePath, LoadOptions.PreserveWhitespace );

        var currentVersionPropertyGroup = currentVersionDocument.Root!.Element( "PropertyGroup" )!;

        foreach ( var dependencyOverride in autoUpdatedDependencies )
        {
            var dependencySource = dependencyOverride.Value;

            var dependency = context.Product.ProductFamily.GetDependencyDefinition( dependencyOverride.Key );

            // Path to the downloaded build version file.
            var dependencyVersionPath = dependencySource.VersionFile;

            if ( dependencyVersionPath == null )
            {
                context.Console.WriteError( $"Version file of '{dependency.Name}' does not exist." );

                return false;
            }

            // Load the up-to-date version file of dependency.
            var dependencyVersionDocument = XDocument.Load( dependencyVersionPath );

            var currentDependencyVersionValue =
                dependencyVersionDocument.Root!.Element( "PropertyGroup" )!.Element( $"{dependency.NameWithoutDot}Version" )!.Value;

            // Load dependency version from public version.
            var versionElementName = $"{dependency.NameWithoutDot}Version";
            var versionElement = currentVersionPropertyGroup.Element( versionElementName );
            
            if ( versionElement == null )
            {
                context.Console.WriteWarning( $"No property '{versionElementName}'." );
            }

            var oldVersionValue = versionElement?.Value;

            // We don't need to rewrite the file if there is no change in version.
            if ( oldVersionValue == currentDependencyVersionValue )
            {
                context.Console.WriteMessage( $"Version of '{dependency.Name}' dependency is up to date." );

                continue;
            }

            if ( versionElement == null )
            {
                versionElement = new( versionElementName );
                currentVersionPropertyGroup.Add( versionElement );
            }

            versionElement.Value = currentDependencyVersionValue;
            dependenciesUpdated = true;

            context.Console.WriteMessage( $"Bumping version dependency '{dependency}' from '{oldVersionValue}' to '{currentDependencyVersionValue}'." );
        }

        if ( dependenciesUpdated )
        {
            context.Console.WriteImportantMessage( $"Writing updated '{autoUpdatedVersionsFileName}'." );
            
            var xmlWriterSettings =
                new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, IndentChars = "    ", Encoding = new UTF8Encoding( false ) };

            using ( var xmlWriter = XmlWriter.Create( autoUpdatedVersionsFilePath, xmlWriterSettings ) )
            {
                currentVersionDocument.Save( xmlWriter );
            }
        }

        return true;
    }

    private static bool TryCommitAndPushBumpedDependenciesVersions( BuildContext context )
    {
        // Adds AutoUpdatedVersions.props with updated dependencies versions to Git staging area.
        if ( !ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"add {context.Product.AutoUpdatedVersionsFilePath}",
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
                "commit -m \"<<DEPENDENCIES_UPDATED>>\"",
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