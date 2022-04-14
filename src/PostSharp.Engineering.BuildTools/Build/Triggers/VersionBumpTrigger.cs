﻿using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build.Triggers;

/// <summary>
/// Generates a build trigger that triggers the build when the source code has changed in the default branch.
/// </summary>
public class VersionBumpTrigger : IBuildTrigger
{
    public VersionBumpTrigger( DependencyDefinition? dependencyDefinition )
    {
        this.DependencyDefinition = dependencyDefinition;
    }

    public DependencyDefinition? DependencyDefinition { get; }

    public bool SuccessfulOnly { get; set; } = true;

    public void GenerateTeamcityCode( TextWriter writer )
    {
        if ( this.DependencyDefinition != null )
        {
            writer.WriteLine(
                $@"
        finishBuildTrigger {{
            buildType = ""{this.DependencyDefinition.BumpBuildType}""
            // Only successful dependency version bump will trigger version bump of this product.
            successfulOnly = {this.SuccessfulOnly.ToString().ToLowerInvariant()}
            branchFilter = ""+:<default>""
        }}        " );
        }
    }
}