namespace PostSharp.Engineering.BuildTools.Build
{
    public class PublishCommand : BaseCommand<PublishOptions>
    {
        protected override bool ExecuteCore( BuildContext context, PublishOptions options ) => context.Product.Publish( context, options );
    }
}