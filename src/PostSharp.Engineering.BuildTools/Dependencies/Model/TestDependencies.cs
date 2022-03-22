namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public static class TestDependencies
{
    public static DependencyDefinition TestDependency { get; } = new(
        "PostSharp.Engineering.Test.Dependency",
        VcsProvider.AzureRepos,
        "postsharp",
        ("Test_PostSharpEngineeringTestDependency_DebugBuild", "Test_PostSharpEngineeringTestDependency_ReleaseBuild", "Test_PostSharpEngineeringTestDependency_PublicBuild") );
    
    public static DependencyDefinition TestProduct { get; } = new(
        "PostSharp.Engineering.Test.TestProduct",
        VcsProvider.AzureRepos,
        "postsharp",
        ("Test_PostSharpEngineeringTestTestProduct_DebugBuild", "Test_PostSharpEngineeringTestTestProduct_ReleaseBuild", "Test_PostSharpEngineeringTestTestProduct_PublicBuild") );
}