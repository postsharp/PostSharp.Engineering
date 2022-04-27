using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public static class TeamCityHelper
{
    private static readonly HttpClient _httpClient = new()
    {
        DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue( "Bearer", Environment.GetEnvironmentVariable( "TEAMCITY_TOKEN" )! ) }
    };

    public static readonly string TeamCityApiBuildsUri = "https://tc.postsharp.net/app/rest/builds";
    public static readonly string TeamcityApiBuildQueueUri = "https://tc.postsharp.net/app/rest/buildQueue";
    public static readonly string TeamCityApiRunningBuildsUri = "https://tc.postsharp.net/app/rest/builds?locator=running:true";

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

    public static bool ScheduleBuild( BuildContext context, string buildTypeId, out string? buildId )
    {
        buildId = null;
        var token = Environment.GetEnvironmentVariable( "TEAMCITY_TOKEN" );

        if ( string.IsNullOrEmpty( token ) )
        {
            context.Console.WriteError( "The TEAMCITY_TOKEN environment variable is not defined." );

            return false;
        }

        TeamcityClient tc = new( token );

        buildId = tc.ScheduleBuild( buildTypeId );

        return true;
    }
    
    public static bool TriggerTeamCityDeploy( BuildContext context, TeamCityCommandSettings settings )
    {
        // TODO: Make sure to wait for the finish of this thing.
        if ( settings.Bump )
        {
            if ( !TriggerTeamCityVersionBump( context, settings ) )
            {
                return false;
            }
        }

        if ( settings.Bump )
        {
            var r = _httpClient.GetAsync( TeamcityApiBuildQueueUri ).Result;
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
        if ( !GetBumpBuildTypeIdFromProductName( context, settings, out var buildTypeId ) )
        {
            return false;
        }

        context.Console.WriteImportantMessage( $"Triggering '{buildTypeId}' on TeamCity." );

        var payload = GetTeamCityBuildQueuePayloadStringInXml( buildTypeId );
        var content = new StringContent( payload, Encoding.UTF8, "application/xml" );

        var httpResponseResult = _httpClient.PostAsync( TeamcityApiBuildQueueUri, content ).Result;
        
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
    
    public static bool TriggerTeamCityVersionBump( BuildContext context, TeamCityCommandSettings settings )
    {
        // We get build type ID of the product from its DependencyDefinition by finding the definition by product name.
        if ( !GetBuildBuildTypeIdFromProductName( context, settings, out var buildTypeId ) )
        {
            return false;
        }
        
        var token = Environment.GetEnvironmentVariable( "TEAMCITY_TOKEN" );

        if ( string.IsNullOrEmpty( token ) )
        {
            context.Console.WriteError( "The TEAMCITY_TOKEN environment variable is not defined." );

            return false;
        }

        TeamcityClient tc = new( token );

        var buildId = tc.ScheduleBuild( buildTypeId );

        Console.WriteLine( buildId );

        if ( string.IsNullOrEmpty( buildId ) )
        {
            context.Console.WriteError( $"No buildId was returned for scheduling '{buildTypeId}'." );

            return false;
        }

        // TODO: While true, if NOT in queue, poll, if not finished, poll. or something.
        while ( !tc.HasBuildFinishedSuccessfully( buildId ) )
        {
            context.Console.WriteMessage( tc.PollRunningBuildStatus( buildId ).ToString() );
            Thread.Sleep( 2000 );
        }

        // var payload = GetTeamCityBuildQueuePayloadStringInXml( buildTypeId );
        // var content = new StringContent( payload, Encoding.UTF8, "application/xml" );
        //
        // var httpResponseResult = _httpClient.PostAsync( _teamcityApiBuildUri, content ).Result;
        //
        // if ( !httpResponseResult.IsSuccessStatusCode )
        // {
        //     context.Console.WriteError( $"Failed to trigger '{buildTypeId}' on TeamCity." );
        //     context.Console.WriteMessage( httpResponseResult.ToString() );
        //
        //     return false;
        // }

        context.Console.WriteSuccess( $"'{buildTypeId}' was added to build queue on TeamCity." );

        return true;
    }

    public static bool GetBumpBuildTypeIdFromProductName( BuildContext context, TeamCityCommandSettings settings, [NotNullWhen( true )] out string? buildTypeId )
    {
        // No Product name specified in command means we are triggering current product.
        if ( string.IsNullOrEmpty( settings.ProductName ) )
        {
            settings.ProductName = context.Product.ProductName;
        }

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

    public static bool GetBuildBuildTypeIdFromProductName( BuildContext context, TeamCityCommandSettings settings, [NotNullWhen( true )] out string? buildTypeId )
    {
        // No Product name specified in command means we are triggering current product.
        if ( string.IsNullOrEmpty( settings.ProductName ) )
        {
            settings.ProductName = context.Product.ProductName;
        }

        buildTypeId = Dependencies.Model.Dependencies.All
            .Where( d => d.Name.Equals( settings.ProductName, StringComparison.OrdinalIgnoreCase ) )
            .Select( d => d.CiBuildTypes[settings.BuildConfiguration] )
            .FirstOrDefault() ?? Dependencies.Model.TestDependencies.All
            .Where( d => d.Name.Equals( settings.ProductName, StringComparison.OrdinalIgnoreCase ) )
            .Select( d => d.CiBuildTypes[settings.BuildConfiguration] )
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