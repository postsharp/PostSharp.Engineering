using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Generic;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public abstract class ArtifactPublisher : Publisher
    {
        public Pattern Files { get; }

        public Tester[] Testers { get; init; } = Array.Empty<Tester>();

        protected ArtifactPublisher( Pattern files )
        {
            this.Files = files;
        }

        protected override bool Publish(
            BuildContext context,
            PublishSettings settings,
            (string Private, string Public) directories,
            BuildConfigurationInfo configuration,
            BuildInfo buildInfo,
            bool isPublic,
            ref bool hasTarget,
            ref bool allFilesSucceeded )
        {
            var success = true;

            var directory = isPublic ? directories.Public : directories.Private;

            var files = new List<FilePatternMatch>();

            if ( !this.Files.TryGetFiles( directory, buildInfo, files ) )
            {
                context.Console.WriteWarning( $"No created artifacts match the required publisher pattern(s)." );

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

                switch ( this.Execute( context, settings, filePath, buildInfo, configuration ) )
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
                foreach ( var tester in this.Testers )
                {
                    switch ( tester.Execute( context, directories.Private, buildInfo, configuration, settings.Dry ) )
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

            if ( !success )
            {
                context.Console.WriteError( "Artifact publishing has failed." );
            }

            return success;
        }

        /// <summary>
        /// Executes the target for a specified artifact.
        /// </summary>
        public abstract SuccessCode Execute(
            BuildContext context,
            PublishSettings settings,
            string file,
            BuildInfo buildInfo,
            BuildConfigurationInfo configuration );
    }
}