namespace PostSharp.Engineering.BuildTools.Build
{
    public class PrepareCommand : BaseCommand<BaseBuildSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BaseBuildSettings options )
        {
            return context.Product.Prepare( context, options );
        }
    }
}