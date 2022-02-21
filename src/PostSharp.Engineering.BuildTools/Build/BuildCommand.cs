namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Builds the product.
    /// </summary>
    public class BuildCommand : BaseCommand<BuildSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BuildSettings settings )
        {
            return context.Product.Build( context, settings );
        }
    }
}