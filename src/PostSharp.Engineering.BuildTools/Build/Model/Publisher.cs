using System;
using System.Collections.Immutable;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public abstract class Publisher
    {
        /// <summary>
        /// Gets a value indicating whether the target support public publishing, i.e. if it should be included
        /// when the <see cref="PublishSettings.Public"/> option is specified.
        /// </summary>
        public abstract bool SupportsPublicPublishing { get; }

        /// <summary>
        /// Gets a value indicating whether the target support private publishing, i.e. if it should be included
        /// when the <see cref="PublishSettings.Public"/> option is not specified.
        /// </summary>
        public abstract bool SupportsPrivatePublishing { get; }

        /// <summary>
        /// Gets the extension of the principal artifacts of this target (e.g. <c>.nupkg</c> for a package).
        /// </summary>
        public abstract string Extension { get; }

        /// <summary>
        /// Executes the target for a specified artefact.
        /// </summary>
        public abstract SuccessCode Execute( BuildContext context, PublishSettings settings, string file, bool isPublic );

        public static ImmutableArray<Publisher> DefaultCollection
            => ImmutableArray.Create<Publisher>(
                new NugetPublisher( "https://api.nuget.org/v3/index.json", "%NUGET_ORG_API_KEY%", true ),
                new VsixPublisher() );

        public static bool PublishDirectory(
            BuildContext context,
            PublishSettings settings,
            string directory,
            bool isPublic )
        {
            var success = true;

            foreach ( var publishingTarget in DefaultCollection )
            {
                foreach ( var file in Directory.EnumerateFiles( directory ) )
                {
                    if ( file.Contains( "-local-", StringComparison.OrdinalIgnoreCase ) )
                    {
                        context.Console.WriteError( "Cannot publish a local build." );

                        return false;
                    }

                    if ( Path.GetExtension( file )
                        .Equals( publishingTarget.Extension, StringComparison.OrdinalIgnoreCase ) )
                    {
                        switch ( publishingTarget.Execute( context, settings, Path.Combine( directory, file ), isPublic ) )
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
            }

            if ( !success )
            {
                context.Console.WriteError( "Publishing has failed." );
            }

            return success;
        }
    }
}