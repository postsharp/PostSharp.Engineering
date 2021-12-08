namespace PostSharp.Engineering.BuildTools.Build
{
    public class PrepareCommand : BaseCommand<BaseBuildSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BaseBuildSettings settings )
        {
            return context.Product.Prepare( context, settings );
        }
    }
}