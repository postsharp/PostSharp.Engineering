namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public enum DependencyConfigurationOrigin
{
    /// <summary>
    /// Unknown origin.
    /// </summary>
    Unknown,

    /// <summary>
    /// Default value defined in source code.
    /// </summary>
    Default,

    /// <summary>
    /// Overridden value using the command-line tool.
    /// </summary>
    Override,

    /// <summary>
    /// Transitive from a parent dependency.
    /// </summary>
    Transitive
}