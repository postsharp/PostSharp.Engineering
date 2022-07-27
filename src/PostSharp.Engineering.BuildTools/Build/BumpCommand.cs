namespace PostSharp.Engineering.BuildTools.Build;

public class BumpCommand : BaseCommand<BumpSettings>
{
    protected override bool ExecuteCore( BuildContext context, BumpSettings settings )
    {
        return context.Product.BumpVersion( context, settings );
    }
}