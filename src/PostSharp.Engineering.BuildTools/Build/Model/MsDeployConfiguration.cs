namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class MsDeployConfiguration
    {
        public ParametricString PackageFileName { get; init; }

        public string SubscriptionId { get; init; }

        public string ResourceGroupName { get; init; }

        public string SiteName { get; init; }

        public string SlotName { get; init; } = "staging";

        public string? VirtualDirectory { get; init; }

        public MsDeployConfiguration(
            ParametricString packageFileName,
            string subscriptionId,
            string resourceGroupName,
            string siteName,
            string? slotName = null,
            string? virtualDirectory = null )
        {
            this.PackageFileName = packageFileName;
            this.SubscriptionId = subscriptionId;
            this.ResourceGroupName = resourceGroupName;
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