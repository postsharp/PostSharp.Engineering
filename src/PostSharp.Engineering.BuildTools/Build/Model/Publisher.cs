using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Generic;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public abstract class Publisher
    {
        protected Publisher( Pattern files )
        {
            this.Files = files;
        }

        public Pattern Files { get; }

        /// <summary>
        /// Executes the target for a specified artifact.
        /// </summary>
        public abstract SuccessCode Execute( BuildContext context, PublishSettings settings, string file, BuildConfigurationInfo configuration );

        public static bool PublishDirectory(
            BuildContext context,
            PublishSettings settings,
            string directory,
            BuildConfigurationInfo configuration,
            VersionInfo version,
            bool isPublic,
            ref bool hasTarget )
        {
            var success = true;

            var publishers = isPublic ? configuration.PublicPublishers : configuration.PrivatePublishers;

            if ( publishers is not { Length: > 0 } )
            {
                return true;
            }

            foreach ( var publisher in publishers )
            {
                var files = new List<FilePatternMatch>();

                if ( !publisher.Files.TryGetFiles( directory, version, files ) )
                {
                    return false;
                }

                foreach ( var file in files )
                {
                    if ( file.Stem.Contains( "-local-", StringComparison.OrdinalIgnoreCase ) )
                    {
                        context.Console.WriteError( "Cannot publish a local build." );

                        return false;
                    }

                    hasTarget = true;

                    var filePath = Path.Combine( directory, file.Path );

                    switch ( publisher.Execute( context, settings, filePath, configuration ) )
                    {
                        case SuccessCode.Success:
                            break;

                        case SuccessCode.Error:
                            success = false;

                            break;

                        case SuccessCode.Fatal:
                            return false;
                    }
                }
            }

            if ( !success )
            {
                context.Console.WriteError( "Publishing has failed." );
            }

            return success;
        }
    }
}