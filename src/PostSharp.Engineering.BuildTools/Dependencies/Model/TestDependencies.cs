using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public static class TestDependencies
{
    public static DependencyDefinition TestProduct { get; } = new(
        "PostSharp.Engineering.Test.TestProduct",
        VcsProvider.AzureRepos,
        "postsharp",
        ("Test_PostSharpEngineeringTestTestProduct_DebugBuild", "Test_PostSharpEngineeringTestTestProduct_ReleaseBuild", "Test_PostSharpEngineeringTestTestProduct_PublicBuild") );
    
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

    public static ImmutableArray<DependencyDefinition> All { get; } = ImmutableArray.Create(
        TestProduct,
        Dependency,
        TransitiveDependency );
}