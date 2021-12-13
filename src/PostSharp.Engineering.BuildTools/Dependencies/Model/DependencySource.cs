namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public sealed class DependencySource
    {
        public string? Branch { get; }

        public int? BuildNumber { get; }

        public string? CiBuildTypeId { get; }

        internal string? VersionFile { get; set; }

        public DependencySourceKind SourceKind { get; }

        public DependencySource( DependencySourceKind sourceKind, string? branch = null )
        {
            this.Branch = branch;
            this.SourceKind = sourceKind;
        }

        // The branch here is just informative. It is not used to resolve the dependency.
        public DependencySource( DependencySourceKind sourceKind, int buildNumber, string? ciBuildTypeId = null, string? branch = null )
        {
            this.SourceKind = sourceKind;
            this.BuildNumber = buildNumber;
            this.CiBuildTypeId = ciBuildTypeId;
            this.Branch = branch;
        }

        public override string ToString()
        {
            if (this.BuildNumber != null)
            {
                return $"{this.SourceKind}, BuildNumber='{this.BuildNumber}', CiBuildTypeId='{this.CiBuildTypeId}', Branch='{this.Branch}'";
            }
            else if ( this.Branch != null )
            {
                return $"{this.SourceKind}, Branch='{this.Branch}'";
            }
            else
            {
                return this.SourceKind.ToString();
            }
        }
    }
}