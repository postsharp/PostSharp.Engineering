namespace PostSharp.Engineering.BuildTools.Build
{
    public class PrepareCommand : BaseCommand<BuildSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BuildSettings settings )
        {
            return context.Product.Prepare( context, settings );
        }
    }
}