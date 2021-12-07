namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public sealed class DependencySource
    {
        public string? Branch { get; }

        public DependencySourceKind SourceKind { get; }

        public DependencySource( DependencySourceKind sourceKind, string? branch = null )
        {
            this.Branch = branch;
            this.SourceKind = sourceKind;
        }

        public override string ToString()
        {
            if ( this.Branch != null )
            {
                return this.SourceKind + ", Branch=" + this.Branch;
            }
            else
            {
                return this.SourceKind.ToString();
            }
        }
    }
}