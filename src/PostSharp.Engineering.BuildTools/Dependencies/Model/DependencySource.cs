namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public sealed class DependencySource
    {
        /// <summary>
        /// Gets the NuGet version of the dependency, if <see cref="SourceKind"/> is set to <see cref="DependencySourceKind.Feed"/>.
        /// </summary>
        public string? Version { get; internal set; }

        public ICiBuildSpec? BuildServerSource { get; internal set; }

        internal string? VersionFile { get; set; }

        public DependencySourceKind SourceKind { get; internal set; }

        public DependencyConfigurationOrigin Origin { get; internal init; }

        public static DependencySource CreateLocal( DependencyConfigurationOrigin origin )
            => new() { Origin = origin, SourceKind = DependencySourceKind.Local };

        public static DependencySource CreateFeed( string? version, DependencyConfigurationOrigin origin )
            => new() { Origin = origin, SourceKind = DependencySourceKind.Feed, Version = version };

        public static DependencySource CreateBuildServerSource( ICiBuildSpec source, DependencyConfigurationOrigin origin )
            => new() { Origin = origin, SourceKind = DependencySourceKind.BuildServer, BuildServerSource = source };

        public override string ToString()
        {
            switch ( this.SourceKind )
            {
                case DependencySourceKind.BuildServer:
                    return $"BuildServer, {this.BuildServerSource}, Origin={this.Origin}";

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