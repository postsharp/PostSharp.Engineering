using System;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class AppServiceSwapper : Swapper
    {
        public int MyProperty { get; set; }

        public override SuccessCode Execute( BuildContext context, SwapSettings settings, BuildConfigurationInfo configuration )
        {
            throw new NotImplementedException();
        }
    }
}