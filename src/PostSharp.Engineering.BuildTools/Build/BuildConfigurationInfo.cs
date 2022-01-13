using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Build;

public record BuildConfigurationInfo(

    // ReSharper disable once InconsistentNaming
    string MSBuildName,
    bool RequiresSigning = false,
    bool PublishArtifacts = false,
    IBuildTrigger[]? BuildTriggers = null );