using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public static class TestDependencies
{
    public static DependencyDefinition TestProduct { get; } = new(
        "PostSharp.Engineering.Test.TestProduct",
        VcsProvider.AzureRepos,
        "Test" );

    public static DependencyDefinition Dependency { get; } = new(
        "PostSharp.Engineering.Test.Dependency",
        VcsProvider.AzureRepos,
        "Test" );

    public static DependencyDefinition TransitiveDependency { get; } = new(
        "PostSharp.Engineering.Test.TransitiveDependency",
        VcsProvider.AzureRepos,
        "Test" );

    public static DependencyDefinition GitHub { get; } = new(
        "PostSharp.Engineering.Test.GitHub",
        VcsProvider.GitHub,
        "Test" );

    public static DependencyDefinition MainVersionDependency { get; } = new(
        "PostSharp.Engineering.Test.MainVersionDependency",
        VcsProvider.AzureRepos,
        "Test" );

    public static DependencyDefinition PatchVersion { get; } = new(
        "PostSharp.Engineering.Test.PatchVersion",
        VcsProvider.AzureRepos,
        "Test" );

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