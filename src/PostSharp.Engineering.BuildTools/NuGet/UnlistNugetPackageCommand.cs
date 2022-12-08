// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Newtonsoft.Json;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;

#pragma warning disable 8765

namespace PostSharp.Engineering.BuildTools.NuGet;

public class UnlistNugetPackageCommand : Command<UnlistNugetPackageCommandSettings>
{
    public override int Execute( CommandContext context, UnlistNugetPackageCommandSettings settings )
    {
        return Execute( new ConsoleHelper(), settings ) ? 0 : 1;
    }
    
    public static bool Execute( ConsoleHelper console, UnlistNugetPackageCommandSettings settings )
    {
        if ( string.IsNullOrEmpty( settings.PackageName ) )
        {
            console.WriteError( "No package specified for unlisting from NuGet." );

            return false;
        }
        
        var packageName = settings.PackageName.ToLowerInvariant();
        
        if ( !TryGetAllPackageVersions( console, packageName, out var versions ) )
        {
            console.WriteError( $"Failed to get all versions of '{settings.PackageName}' nuget package." );

            return false;
        }

        if ( !UnlistPackage( console, packageName, versions ) ) 
        {
            return false;
        }
        
        console.WriteSuccess( $"Successfully unlisted all versions of '{packageName}' package." );

        return true;
    }

    private static bool TryGetAllPackageVersions( ConsoleHelper console, string packageName, [NotNullWhen( true )] out List<string>? versions )
    {
        var httpClient = new HttpClient();

        string? versionsJson = null;

        try
        {
            versionsJson = httpClient.GetStringAsync( $"https://api.nuget.org/v3-flatcontainer/{packageName}/index.json" ).Result;
        }
        catch ( Exception e )
        {
            console.WriteError(
                e.Message.Contains( "404", StringComparison.OrdinalIgnoreCase )
                    ? $"NuGet package '{packageName}' not found."
                    : e.Message );
        }

        if ( string.IsNullOrEmpty( versionsJson ) )
        {
            versions = null;

            return false;
        }

        var deserializedVersions = JsonConvert.DeserializeObject<Versions>( versionsJson );

        if ( deserializedVersions == null || deserializedVersions.VersionsList == null )
        {
            versions = null;

            return false;
        }

        versions = deserializedVersions.VersionsList;

        return true;
    }
    
    private static bool UnlistPackage( ConsoleHelper console, string packageName, List<string> packageVersions )
    {
        console.WriteMessage( $"Unlisting all versions of package '{packageName}'." );

        var nugetApiKey = Environment.ExpandEnvironmentVariables( "%NUGET_ORG_UNLIST_API_KEY%" );
        var nugetServerUrl = Environment.ExpandEnvironmentVariables( "%NUGET_ORG_GET_URL%" );

        var success = true;

        foreach ( var version in packageVersions )
        {
            if ( !ToolInvocationHelper.InvokeTool(
                    console,
                    "dotnet",
                    $"nuget delete {packageName} {version} --api-key {nugetApiKey} --non-interactive --source {nugetServerUrl}",
                    "" ) )
            {
                success = false;
            }
        }

        return success;
    }

    internal class Versions
    {
        [JsonProperty( "versions" )]
        public List<string>? VersionsList { get; set; }
    }
}