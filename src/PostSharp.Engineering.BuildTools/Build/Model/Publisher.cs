﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    /// <summary>
    /// An abstract publisher class used in <see cref="Product.Publish"/> to publish artifacts or execute publishing step.
    /// </summary>
    [PublicAPI]
    public abstract class Publisher
    {
        /// <summary>
        /// When set to false, the publisher will not publish pre-release artifacts. Default is true.
        /// </summary>
        public bool PublishPrerelease { get; init; } = true;
        
        protected abstract bool Publish(
            BuildContext context,
            PublishSettings settings,
            (string Private, string Public) directories,
            BuildConfigurationInfo configuration,
            BuildInfo buildInfo,
            bool isPublic,
            ref bool hasTarget );

        public static bool PublishDirectory(
            BuildContext context,
            PublishSettings settings,
            (string Private, string Public) directories,
            BuildConfigurationInfo configuration,
            BuildInfo buildInfo,
            bool isPublic,
            ref bool hasTarget )
        {
            var publishers = isPublic ? configuration.PublicPublishers : configuration.PrivatePublishers;

            if ( publishers is not { Length: > 0 } )
            {
                return true;
            }
            
            var publishingSucceeded = true;

            foreach ( var publisher in publishers )
            {
                if ( buildInfo.IsPrerelease && !publisher.PublishPrerelease )
                {
                    context.Console.WriteWarning( $"Skip publishing by '{publisher.GetType().Name}' because '{buildInfo.PackageVersion}' is a pre-release." );

                    continue;
                }
                
                if ( !publisher.Publish(
                        context,
                        settings,
                        directories,
                        configuration,
                        buildInfo,
                        isPublic,
                        ref hasTarget ) )
                {
                    publishingSucceeded = false;
                }
            }

            return publishingSucceeded;
        }
    }
}