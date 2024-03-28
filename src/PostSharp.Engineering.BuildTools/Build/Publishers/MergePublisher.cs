// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Dependencies;
using PostSharp.Engineering.BuildTools.Utilities;

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
                $"{nameof(MergePublisher)} can only be executed on the development branch ('{sourceBranch}'). The current branch is '{context.Branch}'." );

            return SuccessCode.Error;
        }
        
        var targetBranch = context.Product.DependencyDefinition.ReleaseBranch;

        if ( targetBranch == null )
        {
            context.Console.WriteWarning( $"Source code not published. The release branch is not set for '{context.Product.ProductName}' product." );

            return SuccessCode.Success;
        }

        // Go through all dependencies and update their fixed version in Versions.props file.
        if ( !AutoUpdatedDependenciesHelper.TryParseAndVerifyDependencies( context, settings, out var dependenciesUpdated ) )
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