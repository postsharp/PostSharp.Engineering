// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public static class TestDependencies
{
    public static DependencyDefinition TestProduct { get; } = new(
        "PostSharp.Engineering.Test.TestProduct",
        VcsProvider.AzureRepos,
        "Engineering" )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            $"Test_Test{MainVersion.ValueWithoutDots}_TestProduct_DebugBuild",
            $"Test_Test{MainVersion.ValueWithoutDots}_TestProduct_ReleaseBuild",
            $"Test_Test{MainVersion.ValueWithoutDots}_TestProduct_PublicBuild" ),
        DeploymentBuildType = $"Test_Test{MainVersion.ValueWithoutDots}_TestProduct_PublicDeployment",
        BumpBuildType = $"Test_Test{MainVersion.ValueWithoutDots}_TestProduct_VersionBump"
    };

    public static DependencyDefinition Dependency { get; } = new(
        "PostSharp.Engineering.Test.Dependency",
        VcsProvider.AzureRepos,
        "Engineering" )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            $"Test_Test{MainVersion.ValueWithoutDots}_Dependency_DebugBuild",
            $"Test_Test{MainVersion.ValueWithoutDots}_Dependency_ReleaseBuild",
            $"Test_Test{MainVersion.ValueWithoutDots}_Dependency_PublicBuild" ),
        DeploymentBuildType = $"Test_Test{MainVersion.ValueWithoutDots}_Dependency_PublicDeployment",
        BumpBuildType = $"Test_Test{MainVersion.ValueWithoutDots}_Dependency_VersionBump"
    };

    public static DependencyDefinition TransitiveDependency { get; } = new(
        "PostSharp.Engineering.Test.TransitiveDependency",
        VcsProvider.AzureRepos,
        "Engineering" )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            $"Test_Test{MainVersion.ValueWithoutDots}_TransitiveDependency_DebugBuild",
            $"Test_Test{MainVersion.ValueWithoutDots}_TransitiveDependency_ReleaseBuild",
            $"Test_Test{MainVersion.ValueWithoutDots}_TransitiveDependency_PublicBuild" ),
        DeploymentBuildType = $"Test_Test{MainVersion.ValueWithoutDots}_TransitiveDependency_PublicDeployment",
        BumpBuildType = $"Test_Test{MainVersion.ValueWithoutDots}_TransitiveDependency_VersionBump"
    };

    public static DependencyDefinition GitHub { get; } = new(
        "PostSharp.Engineering.Test.GitHub",
        VcsProvider.GitHub,
        "postsharp" )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            $"Test_Test{MainVersion.ValueWithoutDots}_GitHub_DebugBuild",
            $"Test_Test{MainVersion.ValueWithoutDots}_GitHub_ReleaseBuild",
            $"Test_Test{MainVersion.ValueWithoutDots}_GitHub_PublicBuild" ),
        DeploymentBuildType = $"Test_Test{MainVersion.ValueWithoutDots}_GitHub_PublicDeployment",
        BumpBuildType = $"Test_Test{MainVersion.ValueWithoutDots}_GitHub_VersionBump"
    };

    public static DependencyDefinition MainVersionDependency { get; } = new(
        "PostSharp.Engineering.Test.MainVersionDependency",
        VcsProvider.AzureRepos,
        "Engineering" )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            $"Test_Test{MainVersion.ValueWithoutDots}_MainVersionDependency_DebugBuild",
            $"Test_Test{MainVersion.ValueWithoutDots}_MainVersionDependency_ReleaseBuild",
            $"Test_Test{MainVersion.ValueWithoutDots}_MainVersionDependency_PublicBuild" ),
        DeploymentBuildType = $"Test_Test{MainVersion.ValueWithoutDots}_MainVersionDependency_PublicDeployment",
        BumpBuildType = $"Test_Test{MainVersion.ValueWithoutDots}_MainVersionDependency_VersionBump"
    };

    public static DependencyDefinition PatchVersion { get; } = new(
        "PostSharp.Engineering.Test.PatchVersion",
        VcsProvider.AzureRepos,
        "Engineering" )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            $"Test_Test{MainVersion.ValueWithoutDots}_PatchVersion_DebugBuild",
            $"Test_Test{MainVersion.ValueWithoutDots}_PatchVersion_ReleaseBuild",
            $"Test_Test{MainVersion.ValueWithoutDots}_PatchVersion_PublicBuild" ),
        DeploymentBuildType = $"Test_Test{MainVersion.ValueWithoutDots}_PatchVersion_PublicDeployment",
        BumpBuildType = $"Test_Test{MainVersion.ValueWithoutDots}_PatchVersion_VersionBump"
    };

    public static DependencyDefinition NopCommerce { get; } = new(
        "Metalama.Tests.NopCommerce",
        VcsProvider.GitHub,
        "postsharp",
        false )
    {
        CiBuildTypes = new ConfigurationSpecific<string>(
            "Metalama_MetalamaTests_MetalamaTestsNopCommerce_DebugBuild",
            "Metalama_MetalamaTests_MetalamaTestsNopCommerce_ReleaseBuild",
            "Metalama_MetalamaTests_MetalamaTestsNopCommerce_PublicBuild" ),
        DeploymentBuildType = "Metalama_MetalamaTests_MetalamaTestsNopCommerce_PublicDeployment",
        BumpBuildType = "Metalama_MetalamaTests_MetalamaTestsNopCommerce_VersionBump"
    };

    public static ImmutableArray<DependencyDefinition> All { get; } = ImmutableArray.Create(
        TestProduct,
        Dependency,
        TransitiveDependency,
        GitHub,
        MainVersionDependency,
        PatchVersion,
        NopCommerce );
}