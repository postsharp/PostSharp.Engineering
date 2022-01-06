﻿namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public sealed class DependencySource
    {
        public string? DefaultVersion { get; internal set; }

        public string? Branch { get; internal set; }

        public int? BuildNumber { get; internal set; }

        public string? CiBuildTypeId { get; internal set; }

        public string? VersionDefiningDependencyName { get; internal set; }

        internal string? VersionFile { get; set; }

        public DependencySourceKind SourceKind { get; internal set; }

        public string Origin { get; internal init; } = "unknown";

        public static DependencySource CreateOfKind( DependencySourceKind kind, string origin ) => new() { Origin = origin, SourceKind = kind };

        public static DependencySource CreateBuildServerSource( string branch, string? ciBuildTypeId, string origin )
            => new() { Origin = origin, SourceKind = DependencySourceKind.BuildServer, Branch = branch, CiBuildTypeId = ciBuildTypeId };

        // The branch here is just informative. It is not used to resolve the dependency.
        public static DependencySource CreateBuildServerSource( int buildNumber, string? ciBuildTypeId, string? branch, string origin )
            => new()
            {
                Origin = origin,
                SourceKind = DependencySourceKind.BuildServer,
                BuildNumber = buildNumber,
                CiBuildTypeId = ciBuildTypeId,
                Branch = branch
            };

        public static DependencySource CreateTransitiveBuildServerSource( string versionDefiningDependencyName, string? defaultVersion, string origin )
            => new()
            {
                Origin = origin,
                SourceKind = DependencySourceKind.Transitive,
                VersionDefiningDependencyName = versionDefiningDependencyName,
                DefaultVersion = defaultVersion
            };

        public override string ToString()
        {
            if ( this.BuildNumber != null )
            {
                return
                    $"{this.SourceKind}, BuildNumber='{this.BuildNumber}', CiBuildTypeId='{this.CiBuildTypeId}', Branch='{this.Branch}', DefaultVersion='{this.DefaultVersion}', Origin='{this.Origin}'";
            }
            else if ( this.Branch != null )
            {
                return
                    $"{this.SourceKind}, Branch='{this.Branch}', CiBuildTypeId='{this.CiBuildTypeId}', DefaultVersion='{this.DefaultVersion}', Origin='{this.Origin}'";
            }
            else if ( this.VersionDefiningDependencyName != null )
            {
                return
                    $"{this.SourceKind}, VersionDefiningDependencyName='{this.VersionDefiningDependencyName}', DefaultVersion='{this.DefaultVersion}', Origin='{this.Origin}'";
            }
            else
            {
                return $"{this.SourceKind}, DefaultVersion='{this.DefaultVersion}', Origin='{this.Origin}'";
            }
        }
    }
}