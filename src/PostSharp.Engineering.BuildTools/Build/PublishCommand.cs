namespace PostSharp.Engineering.BuildTools.Build
{
    public class PublishCommand : BaseCommand<PublishSettings>
    {
        protected override bool ExecuteCore( BuildContext context, PublishSettings settings ) => context.Product.Publish( context, settings );
    }
}