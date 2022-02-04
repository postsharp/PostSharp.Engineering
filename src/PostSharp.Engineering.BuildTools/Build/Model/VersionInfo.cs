namespace PostSharp.Engineering.BuildTools.Build.Model
{
    // ReSharper disable once InconsistentNaming
    public record VersionInfo( string PackageVersion, string Configuration, string MSBuildConfiguration )
    {
        public VersionInfo( string packageVersion, BuildConfiguration configuration, Product product ) : this(
            packageVersion,
            configuration.ToString(),
            product.Configurations[configuration].MSBuildName ) { }
    }
}