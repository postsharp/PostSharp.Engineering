using PostSharp.Engineering.BuildTools.Utilities;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class AppServiceSwapper : Swapper
    {
        public string SubscriptionId { get; init; }

        public string ResourceGroupName { get; init; }

        public string AppServiceName { get; init; }

        public string SourceSlot { get; init; } = "staging";

        public string TargetSlot { get; init; } = "production";

        public AppServiceSwapper( string subscriptionId, string resourceGroupName, string appServiceName, string? sourceSlot = null, string? targetSlot = null )
        {
            this.SubscriptionId = subscriptionId;
            this.ResourceGroupName = resourceGroupName;
            this.AppServiceName = appServiceName;

            if ( sourceSlot != null )
            {
                this.SourceSlot = sourceSlot;
            }

            if ( targetSlot != null )
            {
                this.TargetSlot = targetSlot;
            }
        }

        public override SuccessCode Execute( BuildContext context, SwapSettings settings, BuildConfigurationInfo configuration )
        {
            var args = $"webapp deployment slot swap --subscription {this.SubscriptionId} --resource-group {this.ResourceGroupName} --name {this.AppServiceName} --slot {this.SourceSlot} --target-slot {this.TargetSlot}";

            return AzHelper.Run( context.Console, args, settings.Dry ) ? SuccessCode.Success : SuccessCode.Error;
        }
    }
}