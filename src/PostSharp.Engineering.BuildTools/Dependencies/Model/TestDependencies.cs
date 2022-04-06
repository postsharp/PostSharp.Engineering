using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public static class TestDependencies
{
    public static DependencyDefinition TestProduct { get; } = new(
        "PostSharp.Engineering.Test.TestProduct",
        VcsProvider.AzureRepos,
        "postsharp",
        ("Test_PostSharpEngineeringTestProduct_DebugBuild", "Test_PostSharpEngineeringTestProduct_ReleaseBuild", "Test_PostSharpEngineeringTestProduct_PublicBuild") );
    
    public static DependencyDefinition Dependency { get; } = new(
        "PostSharp.Engineering.Test.Dependency",
        VcsProvider.AzureRepos,
        "postsharp",
        ("Test_PostSharpEngineeringTestDependency_DebugBuild", "Test_PostSharpEngineeringTestDependency_ReleaseBuild", "Test_PostSharpEngineeringTestDependency_PublicBuild") );
    
    public static DependencyDefinition TransitiveDependency { get; } = new(
        "PostSharp.Engineering.Test.TransitiveDependency",
        VcsProvider.AzureRepos,
        "postsharp",
        ("Test_PostSharpEngineeringTestTransitiveDependency_DebugBuild", "Test_PostSharpEngineeringTestTransitiveDependency_ReleaseBuild", "Test_PostSharpEngineeringTestTransitiveDependency_PublicBuild") );

    public static DependencyDefinition GitHub { get; } = new(
        "PostSharp.Engineering.Test.GitHub",
        VcsProvider.GitHub,
        "postsharp",
        ("Test_PostSharpEngineeringTestGitHub_DebugBuild", "Test_PostSharpEngineeringTestGitHub_ReleaseBuild", "Test_PostSharpEngineeringTestGitHub_PublicBuild") );
    
    public static DependencyDefinition MainVersionDependency { get; } = new(
        "PostSharp.Engineering.Test.MainVersionDependency",
        VcsProvider.AzureRepos,
        "postsharp",
        ("Test_PostSharpEngineeringTestMainVersionDependency_DebugBuild", "Test_PostSharpEngineeringTestMainVersionDependency_ReleaseBuild", "Test_PostSharpEngineeringTestMainVersionDependency_PublicBuild") );
    
    public static DependencyDefinition PatchVersion { get; } = new(
        "PostSharp.Engineering.Test.PatchVersion",
        VcsProvider.AzureRepos,
        "postsharp",
        ("Test_PostSharpEngineeringTestPatchVersion_DebugBuild", "Test_PostSharpEngineeringTestPatchVersion_ReleaseBuild", "Test_PostSharpEngineeringTestPatchVersion_PublicBuild") );

    public static ImmutableArray<DependencyDefinition> All { get; } = ImmutableArray.Create(
        TestProduct,
        Dependency,
        TransitiveDependency,
        GitHub,
        MainVersionDependency,
        PatchVersion );
}