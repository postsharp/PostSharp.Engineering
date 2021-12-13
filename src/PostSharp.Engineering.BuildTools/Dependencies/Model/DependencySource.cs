namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public sealed class DependencySource
    {
        public string? Branch { get; internal set; }

        public int? BuildNumber { get; internal set; }

        public string? CiBuildTypeId { get; internal set; }

        public string? VersionDefiningDependencyName { get; internal set; }

        internal string? VersionFile { get; set; }

        public DependencySourceKind SourceKind { get; private init; }

        public string Origin { get; private init; } = "unknown";

        public static DependencySource CreateOfKind( string origin, DependencySourceKind kind )
            => new()
            {
                Origin = origin,
                SourceKind = kind,
            };

        public static DependencySource CreateBuildServerSource( string origin, string branch, string? ciBuildTypeId = null )
            => new()
            {
                Origin = origin,
                SourceKind = DependencySourceKind.BuildServer,
                Branch = branch,
                CiBuildTypeId = ciBuildTypeId
            };

        // The branch here is just informative. It is not used to resolve the dependency.
        public static DependencySource CreateBuildServerSource( string origin, int buildNumber, string? ciBuildTypeId = null, string? branch = null )
            => new()
            {
                Origin = origin,
                SourceKind = DependencySourceKind.BuildServer,
                BuildNumber = buildNumber,
                CiBuildTypeId = ciBuildTypeId,
                Branch = branch
            };

        public static DependencySource CreateTransitiveBuildServerSource( string origin, string versionDefiningDependencyName )
            => new()
            {
                Origin = origin,
                SourceKind = DependencySourceKind.BuildServer,
                VersionDefiningDependencyName = versionDefiningDependencyName
            };

        public override string ToString()
        {
            if (this.BuildNumber != null)
            {
                return $"{this.SourceKind}, BuildNumber='{this.BuildNumber}', CiBuildTypeId='{this.CiBuildTypeId}', Branch='{this.Branch}', Origin='{this.Origin}'";
            }
            else if ( this.Branch != null )
            {
                return $"{this.SourceKind}, Branch='{this.Branch}', CiBuildTypeId='{this.CiBuildTypeId}', Origin='{this.Origin}'";
            }
            else if (this.VersionDefiningDependencyName != null)
            {
                return $"{this.SourceKind}, VersionDefiningDependencyName='{this.VersionDefiningDependencyName}', Origin='{this.Origin}'";
            }
            else
            {
                return $"{this.SourceKind}, Origin='{this.Origin}'";
            }
        }
    }
}