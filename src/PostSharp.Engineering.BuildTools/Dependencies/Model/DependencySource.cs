namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
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

    public sealed class DependencySource
    {
        /// <summary>
        /// Gets the NuGet version of the dependency, if <see cref="SourceKind"/> is set to <see cref="DependencySourceKind.Feed"/>.
        /// </summary>
        public string? Version { get; internal set; }

        public string? Branch { get; internal set; }

        public int? BuildNumber { get; internal set; }

        public string? CiBuildTypeId { get; internal set; }

        internal string? VersionFile { get; set; }

        public DependencySourceKind SourceKind { get; internal set; }

        public DependencyConfigurationOrigin Origin { get; internal init; }

        public static DependencySource CreateLocal( DependencyConfigurationOrigin origin )
            => new() { Origin = origin, SourceKind = DependencySourceKind.Local };

        public static DependencySource CreateFeed( string? version, DependencyConfigurationOrigin origin )
            => new() { Origin = origin, SourceKind = DependencySourceKind.Feed, Version = version };

        public static DependencySource CreateBuildServerSource( string branch, string? ciBuildTypeId, DependencyConfigurationOrigin origin )
            => new() { Origin = origin, SourceKind = DependencySourceKind.BuildServer, Branch = branch, CiBuildTypeId = ciBuildTypeId };

        // The branch here is just informative. It is not used to resolve the dependency.
        public static DependencySource CreateBuildServerSource( int buildNumber, string? ciBuildTypeId, string? branch, DependencyConfigurationOrigin origin )
            => new()
            {
                Origin = origin,
                SourceKind = DependencySourceKind.BuildServer,
                BuildNumber = buildNumber,
                CiBuildTypeId = ciBuildTypeId,
                Branch = branch
            };

        public override string ToString()
        {
            switch ( this.SourceKind )
            {
                case DependencySourceKind.BuildServer:
                    if ( this.BuildNumber != null )
                    {
                        return
                            $"{this.SourceKind}, BuildNumber='{this.BuildNumber}', CiBuildTypeId='{this.CiBuildTypeId}', Origin='{this.Origin}'";
                    }
                    else
                    {
                        return
                            $"{this.SourceKind}, Branch='{this.Branch}', CiBuildTypeId='{this.CiBuildTypeId}', Origin='{this.Origin}'";
                    }

                case DependencySourceKind.Local:
                    {
                        return $"{this.SourceKind}, Origin='{this.Origin}'";
                    }

                case DependencySourceKind.Feed:
                    return $"{this.SourceKind}, Version='{this.Version}', Origin='{this.Origin}'";

                default:
                    return "<Error>";
            }
        }
    }
}