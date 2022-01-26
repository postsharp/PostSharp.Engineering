namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class MsDeployConfiguration
    {
        public ParametricString PackageFileName { get; init; }

        public string SiteName { get; init; }

        public string SlotName { get; init; } = "staging";

        public string? VirtualDirectory { get; init; }

        public MsDeployConfiguration( ParametricString packageFileName, string siteName, string? slotName = null, string? virtualDirectory = null )
        {
            this.PackageFileName = packageFileName;
            this.SiteName = siteName;

            if ( slotName != null )
            {
                this.SlotName = slotName;
            }

            if ( virtualDirectory != null )
            {
                this.VirtualDirectory = virtualDirectory;
            }
        }
    }
}