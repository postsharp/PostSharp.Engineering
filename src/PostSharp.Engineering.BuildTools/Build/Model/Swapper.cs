// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    /// <summary>
    /// A swapper is some logic that swaps a deployment slot (typically a staging one) onto another deployment slop (typically the production one).
    /// </summary>
    public abstract class Swapper
    {
        /// <summary>
        /// Gets or sets the list of testers that should be successfully executed before the swap is initiated.
        /// </summary>
        public Tester[] Testers { get; init; } = [];

        /// <summary>
        /// Executes the swap operation.
        /// </summary>
        public abstract SuccessCode Execute( BuildContext context, SwapSettings settings, BuildConfigurationInfo configuration );
    }
}