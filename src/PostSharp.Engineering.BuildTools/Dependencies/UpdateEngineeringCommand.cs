// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Newtonsoft.Json.Linq;
using PostSharp.Engineering.BuildTools.Build;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PostSharp.Engineering.BuildTools.Dependencies;

public class UpdateEngineeringCommand : BaseCommand<CommonCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, CommonCommandSettings settings )
    {
        var httpClient = new HttpClient();

        var nugetResponse = httpClient.GetAsync( "https://azuresearch-usnc.nuget.org/query?q=PostSharp.Engineering.Sdk&prerelease=true&semVerLevel=2.0.0" )
            .Result;

        var jsonText = nugetResponse.Content.ReadAsStringAsync().Result;
        var json = JsonDocument.Parse( jsonText );

        var versions = json.RootElement.GetProperty( "data" )
            .EnumerateArray()
            .SelectMany( i => i.GetProperty( "versions" ).EnumerateArray() )
            .Select( v => v.GetProperty( "version" ).GetString() )
            .ToList();

        var lastVersion = versions.Last()!;

        // Update global.json.
        context.Console.WriteImportantMessage( $"Updating engineering to version {lastVersion}." );

        var globalJsonPath = Path.Combine( context.RepoDirectory, "global.json" );
        context.Console.WriteMessage( $"Writing '{globalJsonPath}'." );
        var globalJson = JObject.Parse( File.ReadAllText( globalJsonPath ) );
        var globalJsonProperty = globalJson["msbuild-sdks"]?["PostSharp.Engineering.Sdk"];

        if ( globalJsonProperty != null )
        {
            globalJsonProperty.Replace( new JValue( lastVersion ) );
        }
        else
        {
            context.Console.WriteWarning( $"File '{globalJsonPath}' not updated because there is no reference to PostSharp.Engineering.Sdk." );
        }

        // Update Versions.props
        var versionsFilePath = context.Product.VersionsFilePath;
        context.Console.WriteMessage( $"Writing '{versionsFilePath}'." );
        var versionsFile = XDocument.Load( versionsFilePath, LoadOptions.PreserveWhitespace );
        var versionProperties = versionsFile.XPathSelectElements( "/Project/PropertyGroup/PostSharpEngineeringVersion" ).ToList();

        if ( versionProperties.Count == 1 )
        {
            versionProperties[0].Value = lastVersion;
        }
        else
        {
            context.Console.WriteWarning(
                $"File '{versionsFilePath}' not updated because there is was {versionProperties} properties named PostSharpEngineeringVersion." );
        }

        versionsFile.Save( versionsFilePath );

        return true;
    }
}