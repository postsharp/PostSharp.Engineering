using PostSharp.Engineering.BuildTools.Build;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PostSharp.Engineering.BuildTools.Utilities;

public static class TeamCityHelper
{
    private static readonly HttpClient _httpClient = new()
    {
        DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue( "Bearer", Environment.GetEnvironmentVariable( "TEAMCITY_TOKEN" )! ) }
    };
        
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

    public static bool TriggerTeamCityDeploy( BuildContext context, TeamCityCommandSettings settings )
    {
        var productName = string.IsNullOrEmpty( settings.ProductName ) ? context.Product.ProductName : settings.ProductName;
        context.Console.WriteHeading( $"Deploying {productName}" );

        if ( settings.Bump )
        {
            if ( !TriggerTeamCityVersionBump( context, settings ) )
            {
                return false;
            }
        }

        return true;
    }

    public static bool TriggerTeamCityVersionBump( BuildContext context, TeamCityCommandSettings settings )
    {
        var productName = string.IsNullOrEmpty( settings.ProductName ) ? context.Product.ProductName : settings.ProductName;
        context.Console.WriteHeading( $"Bumping {productName}" );

        return true;
    }

    public static bool TriggerTeamCityBuild( BuildContext context, TeamCityCommandSettings settings )
    {
        var buildTypeId = "none";

        if ( !string.IsNullOrEmpty( settings.ProductName ) )
        {
            buildTypeId = Dependencies.Model.Dependencies.All
                .Where( d => d.Name.Equals( settings.ProductName, StringComparison.OrdinalIgnoreCase ) )
                .Select( d => d.CiBuildTypes[settings.BuildConfiguration] )
                .FirstOrDefault();

            if ( string.IsNullOrEmpty( buildTypeId ) )
            {
                context.Console.WriteError( $"Dependency definition for '{settings.ProductName}' doesn't exist." );

                return false;
            }
        }

        return true;
    }
}