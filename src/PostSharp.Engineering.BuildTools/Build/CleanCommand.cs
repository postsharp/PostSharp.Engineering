namespace PostSharp.Engineering.BuildTools.Build
{
    public class CleanCommand : BaseCommand<BaseBuildSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BaseBuildSettings options )
        {
            context.Product.Clean( context, options );

            return true;
        }
    }
}