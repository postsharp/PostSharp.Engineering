// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.S3.Publishers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;

namespace PostSharp.Engineering.BuildTools.Build.Publishers;

public class InvalidatingS3Publisher : S3Publisher
{
    private readonly string _invalidationApiKeyEnvironmentVariableName;
    private readonly string _invalidatedUrlFormat;

    public InvalidatingS3Publisher(
        IReadOnlyCollection<S3PublisherConfiguration> configurations,
        string invalidationApiKeyEnvironmentVariableName,
        string invalidatedUrlFormat ) : base( configurations )
    {
        this._invalidationApiKeyEnvironmentVariableName = invalidationApiKeyEnvironmentVariableName;
        this._invalidatedUrlFormat = invalidatedUrlFormat;
    }

    public override SuccessCode PublishFile(
        BuildContext context,
        PublishSettings settings,
        string file,
        BuildInfo buildInfo,
        BuildConfigurationInfo configuration )
    {
        var docApiKey = Environment.GetEnvironmentVariable( this._invalidationApiKeyEnvironmentVariableName );

        if ( string.IsNullOrEmpty( docApiKey ) )
        {
            context.Console.WriteError( $"The {this._invalidationApiKeyEnvironmentVariableName} environment variable is not defined." );

            return SuccessCode.Fatal;
        }

        var successCode = base.PublishFile( context, settings, file, buildInfo, configuration );

        if ( successCode != SuccessCode.Success )
        {
            return successCode;
        }

        string GetUrl( string apiKey ) => string.Format( CultureInfo.InvariantCulture, this._invalidatedUrlFormat, apiKey );

        context.Console.WriteImportantMessage( $"Invalidating {GetUrl( "***" )}" );

        var url = GetUrl( docApiKey );
        using var httpClient = new HttpClient();
        var invalidationResponse = httpClient.GetAsync( url ).GetAwaiter().GetResult();

        if ( invalidationResponse.StatusCode != System.Net.HttpStatusCode.OK )
        {
            context.Console.WriteError( "Failed to invalidate documentation cache." );

            return SuccessCode.Fatal;
        }

        return SuccessCode.Success;
    }
}