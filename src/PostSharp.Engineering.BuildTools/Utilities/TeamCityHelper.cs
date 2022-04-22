using PostSharp.Engineering.BuildTools.Build;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PostSharp.Engineering.BuildTools.Utilities;

public static class TeamCityHelper
{
    public static HttpClient Client = new()
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
        return true;
    }

    public static bool TriggerTeamCityBuild( BuildContext context, TeamCityCommandSettings settings )
    {
        var build = settings.BuildConfiguration.ToString();
        var requestContent = $@"<build branchName=""master""><buildType id=""Test_PostSharpEngineeringTestTransitiveDependency_{build}Build""/></build>";
        Console.WriteLine( requestContent );
        var response = Client.PostAsync( "https://tc.postsharp.net/app/rest/buildQueue", new StringContent( requestContent ) ).Result;

        Console.WriteLine( response );

        return true;
    }
}