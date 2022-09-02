﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Generic;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    /// <summary>
    /// A publisher that publishes all artifact files specified in <see cref="Files"/> pattern.
    /// </summary>
    public abstract class ArtifactPublisher : Publisher
    {
        public Pattern Files { get; }

        public Tester[] Testers { get; init; } = Array.Empty<Tester>();

        protected ArtifactPublisher( Pattern files )
        {
            this.Files = files;
        }

        /// <summary>
        /// Executes the target for a specified artifact.
        /// </summary>
        public abstract SuccessCode PublishFile(
            BuildContext context,
            PublishSettings settings,
            string file,
            BuildInfo buildInfo,
            BuildConfigurationInfo configuration );

        protected sealed override bool Publish(
            BuildContext context,
            PublishSettings settings,
            (string Private, string Public) directories,
            BuildConfigurationInfo configuration,
            BuildInfo buildInfo,
            bool isPublic,
            ref bool hasTarget )
        {
            var success = true;

            var directory = isPublic ? directories.Public : directories.Private;

            var files = new List<FilePatternMatch>();

            if ( !this.Files.TryGetFiles( directory, buildInfo, files ) )
            {
                context.Console.WriteWarning( $"Created artifact files do not match the publisher pattern(s): '{this.Files}'" );

                return true;
            }

            var allFilesSucceeded = true;

            foreach ( var file in files )
            {
                if ( file.Stem.Contains( "-local-", StringComparison.OrdinalIgnoreCase ) )
                {
                    context.Console.WriteError( "Cannot publish a local build." );

                    return false;
                }

                hasTarget = true;

                var filePath = Path.Combine( directory, file.Path );

                switch ( this.PublishFile( context, settings, filePath, buildInfo, configuration ) )
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
    }
}