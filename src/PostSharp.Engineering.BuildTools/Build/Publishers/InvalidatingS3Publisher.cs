// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.S3.Publishers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;

namespace PostSharp.Engineering.BuildTools.Build.Publishers;

[PublicAPI]
public class InvalidatingS3Publisher : S3Publisher
{
    public InvalidatingS3Publisher(
        IReadOnlyCollection<S3PublisherConfiguration> configurations,
        string invalidationApiKeyEnvironmentVariableName,
        string invalidatedUrlFormat ) : base( configurations )
    {
        this.PublishingStarted += args =>
        {
            var docApiKey = Environment.GetEnvironmentVariable( invalidationApiKeyEnvironmentVariableName );

            if ( string.IsNullOrEmpty( docApiKey ) )
            {
                args.Context.Console.WriteError( $"The {invalidationApiKeyEnvironmentVariableName} environment variable is not defined." );

                args.Success = false;
            }
        };

        this.PublishingCompleted += args =>
        {
            if ( !args.Success )
            {
                return;
            }

            var docApiKey = Environment.GetEnvironmentVariable( invalidationApiKeyEnvironmentVariableName )!;

            string GetUrl( string apiKey ) => string.Format( CultureInfo.InvariantCulture, invalidatedUrlFormat, apiKey );

            var loggedUrl = GetUrl( "***" );

            args.Context.Console.WriteImportantMessage( $"Invalidating {loggedUrl}" );

            var url = GetUrl( docApiKey );
            using var httpClient = new HttpClient();
            var invalidationResponse = httpClient.GetAsync( url ).GetAwaiter().GetResult();

            if ( invalidationResponse.StatusCode != System.Net.HttpStatusCode.OK )
            {
                args.Context.Console.WriteError( $"Failed to invalidate {loggedUrl}." );

                args.Success = false;
            }
        };
    }
}