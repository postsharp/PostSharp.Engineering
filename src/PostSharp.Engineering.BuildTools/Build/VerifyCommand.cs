namespace PostSharp.Engineering.BuildTools.Build;

public class VerifyCommand : BaseCommand<PublishSettings>
{
    protected override bool ExecuteCore( BuildContext context, PublishSettings settings )
    {
        return context.Product.Verify( context, settings );
    }
}