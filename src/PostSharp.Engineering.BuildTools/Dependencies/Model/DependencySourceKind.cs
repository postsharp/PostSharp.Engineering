namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public enum DependencySourceKind
    {
        /// <summary>
        /// Means that the package source is a NuGet feed, which should be, typically, registered in nuget.config.
        /// </summary>
        Feed,

        /// <summary>
        /// Means that the package source is the local artefacts directory of a local repo.
        /// </summary>
        Local,

        /// <summary>
        /// Means that the package source is a build artifact set of a continuous integration build.
        /// </summary>
        BuildServer
    }
}