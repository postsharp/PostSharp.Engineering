using PostSharp.Engineering.BuildTools.Utilities;
using System;

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
            var exe = "az";

            const string identityUserNameEnvironmentVariableName = "AZ_SWAP_IDENTITY_USERNAME";
            var identityUserName = Environment.GetEnvironmentVariable( identityUserNameEnvironmentVariableName );

            if ( identityUserName == null )
            {
                context.Console.WriteImportantMessage( $"{identityUserNameEnvironmentVariableName}" );
            }
            else
            {
                var loginArgs = $"login --identity --username {identityUserName}";

                if ( settings.Dry )
                {
                    context.Console.WriteImportantMessage( $"Dry run: {exe} {loginArgs}" );

                    return SuccessCode.Success;
                }
                else
                {
                    return ToolInvocationHelper.InvokeTool(
                            context.Console,
                            exe,
                            loginArgs,
                            Environment.CurrentDirectory )
                        ? SuccessCode.Success
                        : SuccessCode.Error;
                }
            }

            var args = $"webapp deployment slot swap --subscription {this.SubscriptionId} --resource-group {this.ResourceGroupName} --name {this.AppServiceName} --slot {this.SourceSlot} --target-slot {this.TargetSlot}";

            if ( settings.Dry )
            {
                context.Console.WriteImportantMessage( $"Dry run: {exe} {args}" );

                return SuccessCode.Success;
            }
            else
            {
                return ToolInvocationHelper.InvokeTool(
                        context.Console,
                        exe,
                        args,
                        Environment.CurrentDirectory )
                    ? SuccessCode.Success
                    : SuccessCode.Error;
            }
        }
    }
}