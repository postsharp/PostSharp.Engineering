namespace PostSharp.Engineering.BuildTools.Build
{
    public class TestCommand : BaseCommand<BuildOptions>
    {
        protected override bool ExecuteCore( BuildContext context, BuildOptions options )
        {
            return context.Product.Test( context, options );
        }
    }
}