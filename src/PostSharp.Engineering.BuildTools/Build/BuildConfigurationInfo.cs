namespace PostSharp.Engineering.BuildTools.Build;

/// <summary>
/// Definition of a <see cref="BuildConfiguration"/>.
/// </summary>
/// <param name="MSBuildName">Name of the MSBuild or <c>dotnet build</c> configuration.</param>
/// <param name="RequiresSigning"></param>
/// <param name="PublishArtifacts"></param>
/// <param name="AutoBuild"><c>true</c> when a build of this configuration should be triggered when a commit is pushed to the default branch.</param>
public record BuildConfigurationInfo(

    // ReSharper disable once InconsistentNaming
    string MSBuildName,
    bool RequiresSigning = false,
    bool PublishArtifacts = false,
    bool AutoBuild = false );