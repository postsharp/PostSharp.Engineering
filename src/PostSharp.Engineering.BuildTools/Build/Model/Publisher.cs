// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    /// <summary>
    /// An abstract publisher class used in <see cref="Product.Publish"/> to publish artifacts or execute publishing step.
    /// </summary>
    public abstract class Publisher
    {
        public event Action<PublishEventArgs>? PublishingStarted;
        
        public event Action<PublishEventArgs>? PublishingCompleted;
        
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
            var publishingSucceeded = true;

            var publishers = isPublic ? configuration.PublicPublishers : configuration.PrivatePublishers;

            if ( publishers is not { Length: > 0 } )
            {
                return true;
            }

            foreach ( var publisher in publishers )
            {
                var publishingArgs = new PublishEventArgs(
                    context,
                    settings,
                    directories,
                    configuration,
                    buildInfo,
                    isPublic );

                publisher.PublishingStarted?.Invoke( publishingArgs );

                if ( publishingArgs.Success )
                {
                    publishingArgs.Success = publisher.Publish(
                        context,
                        settings,
                        directories,
                        configuration,
                        buildInfo,
                        isPublic,
                        ref hasTarget );
                }

                publisher.PublishingCompleted?.Invoke(
                    new(
                        context,
                        settings,
                        directories,
                        configuration,
                        buildInfo,
                        isPublic ) );
                
                if ( !publishingArgs.Success )
                {
                    publishingSucceeded = false;
                }
            }

            return publishingSucceeded;
        }
    }
}