// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Docker;

public abstract class DockerRunCommand : BaseCommand<BuildSettings>
{
    protected override bool ExecuteCore( BuildContext context, BuildSettings settings )
    {
        if ( !DockerPrepareCommand.TryPrepare( context, settings, out var imageName ) )
        {
            return false;
        }

        var containerName = imageName + "-" + Random.Shared.NextInt64().ToString( "x", CultureInfo.InvariantCulture );

        var product = context.Product;
        context.Console.WriteHeading( $"Building {product.ProductName} from docker." );

        var arguments = this.GetArguments( settings, context, containerName, imageName );
        context.Console.WriteImportantMessage( "docker " + arguments );

        // Run Docker.
        using ( ConsoleHelper.CancellationToken.Register( KillDocker ) )
        {
            var process = Process.Start( new ProcessStartInfo( "docker", Environment.ExpandEnvironmentVariables( arguments ) ) { UseShellExecute = false } );
            process!.WaitForExit();

            return process.ExitCode == 0;
        }

        void KillDocker()
        {
            ToolInvocationHelper.InvokeTool( context.Console, "docker", $"kill {containerName}" );
        }
    }

    private string GetArguments( BuildSettings settings, BuildContext context, string containerName, string imageName )
    {
        var product = context.Product;
        var artifactsDirectory = Path.Combine( context.RepoDirectory, "artifacts" );

        // Add basic options.
        var argumentList = new List<string>()
        {
            "run",
            "-t",
            "-v cache.nuget:/root/.nuget/packages",
            "-v cache.build-artifacts:/tmp/.build-artifacts/",
            $"--mount type=bind,source={artifactsDirectory},target=/src/{product.ProductName}/artifacts",
            $"--mount type=bind,source={PathHelper.GetEngineeringDataDirectory()},target=/root/.local/PostSharp.Engineering",
            $"--name {containerName}"
        };

        // Add command options.
        argumentList.AddRange( this.Options );

        // Pass environment variables.
        string[] environmentVariables =
        [
            "AWS_ACCESS_KEY_ID",
            "AWS_SECRET_ACCESS_KEY",
            "AZ_IDENTITY_USERNAME",
            "AZURE_CLIENT_ID",
            "AZURE_CLIENT_SECRET",
            "AZURE_DEVOPS_TOKEN",
            "AZURE_TENANT_ID",
            "DOC_API_KEY",
            "GITHUB_REVIEWER_TOKEN",
            "GITHUB_TOKEN",
            "IS_POSTSHARP_OWNED",
            "IS_TEAMCITY_AGENT",
            "MetalamaLicense",
            "NODE_OPTIONS",
            "NUGET_ORG_API_KEY",
            "NUGET_ORG_GET_URL",
            "PostSharpLicense",
            "SIGNSERVER_SECRET",
            "SOURCE_CODE_READING_TOKEN",
            "SOURCE_CODE_WRITING_TOKEN",
            "TEAMCITY_CLOUD_TOKEN",
            "TYPESENSE_API_KEY",
            "VS_MARKETPLACE_ACCESS_TOKEN",
            "VSS_NUGET_EXTERNAL_FEED_ENDPOINTS"
        ];

        foreach ( var variable in environmentVariables )
        {
            if ( !string.IsNullOrWhiteSpace( Environment.GetEnvironmentVariable( variable ) ) )
            {
                argumentList.Add( $"-e {variable}=\"%{variable}%\"" );
            }
        }

        // Add final arguments.
        argumentList.Add( imageName );
        argumentList.Add( this.GetCommand( settings ) );

        var arguments = string.Join( " ", argumentList );

        return arguments;
    }

    protected abstract string GetCommand( BuildSettings settings );

    protected virtual string[] Options => [];
}