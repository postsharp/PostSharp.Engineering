namespace PostSharp.Engineering.BuildTools.Build;

public class BumpCommand : BaseCommand<BaseBuildSettings>
{
    protected override bool ExecuteCore( BuildContext context, BaseBuildSettings settings )
    {
        return context.Product.BumpVersion( context, settings );
    }
}