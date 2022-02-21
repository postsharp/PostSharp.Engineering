namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Swaps two deployment slots.
    /// </summary>
    public class SwapCommand : BaseCommand<SwapSettings>
    {
        protected override bool ExecuteCore( BuildContext context, SwapSettings settings ) => context.Product.Swap( context, settings );
    }
}