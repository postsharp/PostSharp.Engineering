namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public abstract class Tester
    {
        public abstract SuccessCode Execute(
            BuildContext context,
            string artifactsDirectory,
            BuildInfo buildInfo,
            BuildConfigurationInfo configuration,
            bool dry );
    }
}