namespace PostSharp.Engineering.BuildTools.Build;

public class BumpCommand : BaseCommand<BuildSettings>
{
    protected override bool ExecuteCore( BuildContext context, BuildSettings settings )
    {
        return context.Product.BumpVersion( context, settings );
    }
}