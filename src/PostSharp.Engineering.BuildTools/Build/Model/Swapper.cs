using System;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public abstract class Swapper
    {
        public Tester[] Testers { get; init; } = Array.Empty<Tester>();

        public abstract SuccessCode Execute( BuildContext context, SwapSettings settings, BuildConfigurationInfo configuration );
    }
}