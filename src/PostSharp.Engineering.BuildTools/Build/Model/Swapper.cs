namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public abstract class Swapper
    {
        public abstract SuccessCode Execute( BuildContext context, SwapSettings settings, BuildConfigurationInfo configuration );
    }
}