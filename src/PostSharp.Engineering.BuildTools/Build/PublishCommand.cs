﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Publishes (deploys) the artifacts to feeds, marketplaces, or deployment slots.
    /// </summary>
    public class PublishCommand : BaseCommand<PublishSettings>
    {
        protected override bool ExecuteCore( BuildContext context, PublishSettings settings ) => context.Product.Publish( context, settings );
    }
}