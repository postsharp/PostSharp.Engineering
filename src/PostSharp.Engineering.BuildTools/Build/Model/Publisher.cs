namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public abstract class Publisher
    {
        protected abstract bool Publish(
            BuildContext context,
            PublishSettings settings,
            (string Private, string Public) directories,
            BuildConfigurationInfo configuration,
            BuildInfo buildInfo,
            bool isPublic,
            ref bool hasTarget,
            ref bool allFilesSucceeded );

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

            var allFilesSucceeded = true;

            foreach ( var publisher in publishers )
            {
                if ( !publisher.Publish(
                        context,
                        settings,
                        directories,
                        configuration,
                        buildInfo,
                        isPublic,
                        ref hasTarget,
                        ref allFilesSucceeded ) )
                {
                    publishingSucceeded = false;
                }
            }

            return publishingSucceeded;
        }
    }
}