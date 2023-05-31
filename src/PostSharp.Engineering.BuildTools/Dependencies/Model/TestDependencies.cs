// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public static class TestDependencies
{
    private const string _branch = "master";

    public static ProductFamily ProductFamily { get; } = new( "Test" );

    public static DependencyDefinition TestProduct { get; } = new(
        ProductFamily,
        "PostSharp.Engineering.Test.TestProduct",
        _branch,
        VcsProvider.AzureRepos,
        "Engineering" )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            "Test_PostSharpEngineeringTestTestProduct_DebugBuild",
            "Test_PostSharpEngineeringTestTestProduct_ReleaseBuild",
            "Test_PostSharpEngineeringTestTestProduct_PublicBuild" ),
        DeploymentBuildType = "Test_PostSharpEngineeringTestTestProduct_PublicDeployment",
        BumpBuildType = "Test_PostSharpEngineeringTestTestProduct_VersionBump"
    };

    public static DependencyDefinition Dependency { get; } = new(
        ProductFamily,
        "PostSharp.Engineering.Test.Dependency",
        _branch,
        VcsProvider.AzureRepos,
        "Engineering" )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            "Test_PostSharpEngineeringTestDependency_DebugBuild",
            "Test_PostSharpEngineeringTestDependency_ReleaseBuild",
            "Test_PostSharpEngineeringTestDependency_PublicBuild" ),
        DeploymentBuildType = "Test_PostSharpEngineeringTestDependency_PublicDeployment",
        BumpBuildType = "Test_PostSharpEngineeringTestDependency_VersionBump"
    };

    public static DependencyDefinition TransitiveDependency { get; } = new(
        ProductFamily,
        "PostSharp.Engineering.Test.TransitiveDependency",
        _branch,
        VcsProvider.AzureRepos,
        "Engineering" )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            "Test_PostSharpEngineeringTestTransitiveDependency_DebugBuild",
            "Test_PostSharpEngineeringTestTransitiveDependency_ReleaseBuild",
            "Test_PostSharpEngineeringTestTransitiveDependency_PublicBuild" ),
        DeploymentBuildType = "Test_PostSharpEngineeringTestTransitiveDependency_PublicDeployment",
        BumpBuildType = "Test_PostSharpEngineeringTestTransitiveDependency_VersionBump"
    };

    public static DependencyDefinition GitHub { get; } = new(
        ProductFamily,
        "PostSharp.Engineering.Test.GitHub",
        _branch,
        VcsProvider.GitHub,
        "postsharp" )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            "Test_PostSharpEngineeringTestGitHub_DebugBuild",
            "Test_PostSharpEngineeringTestGitHub_ReleaseBuild",
            "Test_PostSharpEngineeringTestGitHub_PublicBuild" ),
        DeploymentBuildType = "Test_PostSharpEngineeringTestGitHub_PublicDeployment",
        BumpBuildType = "Test_PostSharpEngineeringTestGitHub_VersionBump"
    };

    public static DependencyDefinition MainVersionDependency { get; } = new(
        ProductFamily,
        "PostSharp.Engineering.Test.MainVersionDependency",
        _branch,
        VcsProvider.AzureRepos,
        "Engineering" )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            "Test_PostSharpEngineeringTestMainVersionDependency_DebugBuild",
            "Test_PostSharpEngineeringTestMainVersionDependency_ReleaseBuild",
            "Test_PostSharpEngineeringTestMainVersionDependency_PublicBuild" ),
        DeploymentBuildType = "Test_PostSharpEngineeringTestMainVersionDependency_PublicDeployment",
        BumpBuildType = "Test_PostSharpEngineeringTestMainVersionDependency_VersionBump"
    };

    public static DependencyDefinition PatchVersion { get; } = new(
        ProductFamily,
        "PostSharp.Engineering.Test.PatchVersion",
        _branch,
        VcsProvider.AzureRepos,
        "Engineering" )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            "Test_PostSharpEngineeringTestPatchVersion_DebugBuild",
            "Test_PostSharpEngineeringTestPatchVersion_ReleaseBuild",
            "Test_PostSharpEngineeringTestPatchVersion_PublicBuild" ),
        DeploymentBuildType = "Test_PostSharpEngineeringPatchVersion_PublicDeployment",
        BumpBuildType = "Test_PostSharpEngineeringTestPatchVersion_VersionBump"
    };

    public static ImmutableArray<DependencyDefinition> All { get; } = ImmutableArray.Create(
        TestProduct,
        Dependency,
        TransitiveDependency,
        GitHub,
        MainVersionDependency,
        PatchVersion );
}