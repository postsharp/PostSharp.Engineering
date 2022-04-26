using PostSharp.Engineering.BuildTools.Build;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Utilities;

public static class TeamCityHelper
{
    private static readonly HttpClient _httpClient = new()
    {
        DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue( "Bearer", Environment.GetEnvironmentVariable( "TEAMCITY_TOKEN" )! ) }
    };

    private static readonly string _teamcityApiBuildUri = $"https://tc.postsharp.net/app/rest/buildQueue";

    public static bool IsTeamCityBuild( BaseBuildSettings settings )
        => settings.ContinuousIntegration || Environment.GetEnvironmentVariable( "TEAMCITY_GIT_PATH" ) != null;

    public static bool TryGetTeamCitySourceWriteToken( out string environmentVariableName, [NotNullWhen( true )] out string? teamCitySourceWriteToken )
    {
        environmentVariableName = "TEAMCITY_SOURCE_WRITE_TOKEN";
        teamCitySourceWriteToken = Environment.GetEnvironmentVariable( environmentVariableName );

        if ( teamCitySourceWriteToken == null )
        {
            return false;
        }

        return true;
    }

    public static string GetTeamCityBuildQueuePayloadStringInXml( string buildTypeId )
    {
        return $"<build><buildType id=\"{buildTypeId}\" /><comment><text>This build was triggered from console command.</text></comment></build>";
    }

    // TODO: Make async to be able to determine when something is finished.
    // TODO: Check that another bump is not running currently.
    public static bool TriggerTeamCityDeploy( BuildContext context, TeamCityCommandSettings settings )
    {
        // TODO: Make sure to wait for the finish of this thing.
        if ( settings.Bump )
        {
            if ( !TriggerTeamCityVersionBump( context, settings, out var buildId ) )
            {
                return false;
            }
        }

        if ( settings.Bump )
        {
            var r = _httpClient.GetAsync( _teamcityApiBuildUri ).Result;
            var x = r.Content.ReadAsStringAsync( ConsoleHelper.CancellationToken ).Result;
            Console.WriteLine( x );

            return true;
        }

        // No Product name specified in command means we are triggering current product.
        if ( string.IsNullOrEmpty( settings.ProductName ) )
        {
            settings.ProductName = context.Product.ProductName;
        }

        // We get build type ID of the product from its DependencyDefinition by finding the definition by product name.
        if ( !GetBuildTypeIdFromProductName( context, settings, out var buildTypeId ) )
        {
            return false;
        }

        context.Console.WriteImportantMessage( $"Triggering '{buildTypeId}' on TeamCity." );

        var payload = GetTeamCityBuildQueuePayloadStringInXml( buildTypeId );
        var content = new StringContent( payload, Encoding.UTF8, "application/xml" );

        var httpResponseResult = _httpClient.PostAsync( _teamcityApiBuildUri, content ).Result;
        
        if ( !httpResponseResult.IsSuccessStatusCode )
        {
            context.Console.WriteError( $"Failed to trigger '{buildTypeId}' on TeamCity." );
            context.Console.WriteMessage( httpResponseResult.ToString() );

            return false;
        }

        context.Console.WriteMessage( httpResponseResult.ToString() );
        context.Console.WriteSuccess( $"'{buildTypeId}' was added to build queue on TeamCity." );

        return true;
    }

    // TODO: Make async to be able to determine when something is finished.
    // TODO: Check that another bump is not running currently.
    public static bool TriggerTeamCityVersionBump( BuildContext context, TeamCityCommandSettings settings, [NotNullWhen( true )] out string? enqueuedBuildId )
    {
        enqueuedBuildId = null;

        // No Product name specified in command means we are triggering current product.
        if ( string.IsNullOrEmpty( settings.ProductName ) )
        {
            settings.ProductName = context.Product.ProductName;
        }

        // We get build type ID of the product from its DependencyDefinition by finding the definition by product name.
        if ( !GetBuildTypeIdFromProductName( context, settings, out var buildTypeId ) )
        {
            return false;
        }

        context.Console.WriteImportantMessage( $"Triggering '{buildTypeId}' on TeamCity." );

        var payload = GetTeamCityBuildQueuePayloadStringInXml( buildTypeId );
        var content = new StringContent( payload, Encoding.UTF8, "application/xml" );

        var httpResponseResult = _httpClient.PostAsync( _teamcityApiBuildUri, content ).Result;
        
        if ( !httpResponseResult.IsSuccessStatusCode )
        {
            context.Console.WriteError( $"Failed to trigger '{buildTypeId}' on TeamCity." );
            context.Console.WriteMessage( httpResponseResult.ToString() );

            return false;
        }

        enqueuedBuildId = GetBuildIdFromHttpResponse( context, httpResponseResult);

        if ( string.IsNullOrEmpty( enqueuedBuildId ) )
        {
            return false;
        }

        context.Console.WriteSuccess( $"'{buildTypeId}' was added to build queue on TeamCity." );

        return true;
    }

    public static bool GetBuildTypeIdFromProductName( BuildContext context, TeamCityCommandSettings settings, [NotNullWhen( true )] out string? buildTypeId )
    {
        buildTypeId = Dependencies.Model.Dependencies.All
            .Where( d => d.Name.Equals( settings.ProductName, StringComparison.OrdinalIgnoreCase ) )
            .Select( d => d.BumpBuildType )
            .FirstOrDefault() ?? Dependencies.Model.TestDependencies.All
            .Where( d => d.Name.Equals( settings.ProductName, StringComparison.OrdinalIgnoreCase ) )
            .Select( d => d.BumpBuildType )
            .FirstOrDefault();

        if ( string.IsNullOrEmpty( buildTypeId ) )
        {
            context.Console.WriteError( $"Dependency definition for '{settings.ProductName}' doesn't exist." );

            return false;
        }

        return true;
    }
    
    // TODO: read buildId
    public static string? GetBuildIdFromHttpResponse( BuildContext context, HttpResponseMessage httpResponseMessage )
    {
        var httpResponseMessageContentString = httpResponseMessage.Content.ReadAsStringAsync( ConsoleHelper.CancellationToken ).Result;

        var document = XDocument.Parse( httpResponseMessageContentString );
        var build = document.Root;

        return build!.Attribute( "id" )!.Value;
    }
    
    // TODO: check whether buildId is enqueued
    
    // TODO: check whether buildId has finished SUCCESSFULLY
}