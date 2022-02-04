namespace PostSharp.Engineering.BuildTools.Build
{
    public class SwapCommand : BaseCommand<SwapSettings>
    {
        protected override bool ExecuteCore( BuildContext context, SwapSettings settings ) => context.Product.Swap( context, settings );
    }
}