namespace PostSharp.Engineering.BuildTools.Build
{
    public class CleanCommand : BaseCommand<BaseBuildSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BaseBuildSettings settings )
        {
            context.Product.Clean( context, settings );

            return true;
        }
    }
}