using PostSharp.Engineering.BuildTools.Build.Model;

namespace PostSharp.Engineering.BuildTools.Build;

public record BuildConfigurationInfo(

    // ReSharper disable once InconsistentNaming
    string MSBuildName,
    bool RequiresSigning = false,
    IBuildTrigger[]? BuildTriggers = null,

    // Publishers for public artifacts.
    Publisher[]? PublicPublishers = null,

    // Publishers for private artifacts.
    Publisher[]? PrivatePublishers = null,
    Swapper[]? Swappers = null,
    
    string? TeamCityBuildName = null,
    string? TeamCityDeploymentName = null,
    string? TeamCitySwapName = null);