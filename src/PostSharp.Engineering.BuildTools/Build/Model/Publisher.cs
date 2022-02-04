using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Generic;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public abstract class Publisher
    {
        public Pattern Files { get; }

        public Tester[] Testers { get; init; } = Array.Empty<Tester>();

        protected Publisher( Pattern files )
        {
            this.Files = files;
        }

        /// <summary>
        /// Executes the target for a specified artifact.
        /// </summary>
        public abstract SuccessCode Execute(
            BuildContext context,
            PublishSettings settings,
            string file,
            VersionInfo version,
            BuildConfigurationInfo configuration );

        public static bool PublishDirectory(
            BuildContext context,
            PublishSettings settings,
            (string Private, string Public) directories,
            BuildConfigurationInfo configuration,
            VersionInfo version,
            bool isPublic,
            ref bool hasTarget )
        {
            var success = true;

            var publishers = isPublic ? configuration.PublicPublishers : configuration.PrivatePublishers;
            var directory = isPublic ? directories.Public : directories.Private;

            if ( publishers is not { Length: > 0 } )
            {
                return true;
            }

            var allFilesSucceeded = true;

            foreach ( var publisher in publishers )
            {
                var files = new List<FilePatternMatch>();

                if ( !publisher.Files.TryGetFiles( directory, version, files ) )
                {
                    continue;
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

                    switch ( publisher.Execute( context, settings, filePath, version, configuration ) )
                    {
                        case SuccessCode.Success:
                            break;

                        case SuccessCode.Error:
                            success = false;
                            allFilesSucceeded = false;

                            break;

                        case SuccessCode.Fatal:
                            return false;

                        default:
                            throw new NotImplementedException();
                    }
                }

                if ( allFilesSucceeded )
                {
                    foreach ( var tester in publisher.Testers )
                    {
                        switch ( tester.Execute( context, directories.Private, version, configuration, settings.Dry ) )
                        {
                            case SuccessCode.Success:
                                break;

                            case SuccessCode.Error:
                                success = false;

                                break;

                            case SuccessCode.Fatal:
                                return false;

                            default:
                                throw new NotImplementedException();
                        }
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