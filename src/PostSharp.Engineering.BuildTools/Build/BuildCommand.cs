namespace PostSharp.Engineering.BuildTools.Build
{
    public class BuildCommand : BaseCommand<BuildOptions>
    {
        protected override bool ExecuteCore( BuildContext context, BuildOptions options )
        {
            return context.Product.Build( context, options );
        }
    }
}