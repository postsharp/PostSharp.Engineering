// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Docker;

public abstract class DockerRunCommand : BaseCommand<DockerSettings>
{
    protected override bool ExecuteCore( BuildContext context, DockerSettings settings )
    {
        if ( !DockerPrepareCommand.TryPrepare( context, settings, out var imageName, out var baseImage ) )
        {
            return false;
        }

        ToolInvocationHelper.InvokeTool( context.Console, "docker", $"volume create cache.nuget" );
        ToolInvocationHelper.InvokeTool( context.Console, "docker", $"volume create cache.build-artifacts" );

        var containerName = imageName + "-" + Random.Shared.NextInt64().ToString( "x", CultureInfo.InvariantCulture );

        var product = context.Product;
        context.Console.WriteHeading( $"Building {product.ProductName} inside the '{containerName}' container." );

        // Run the configuration script.

        try
        {
            var arguments = this.GetArguments( settings, context, containerName, imageName, baseImage );
            context.Console.WriteImportantMessage( "docker " + arguments );

            using ( ConsoleHelper.CancellationToken.Register( KillDocker ) )
            {
                var process = Process.Start(
                    new ProcessStartInfo( "docker", Environment.ExpandEnvironmentVariables( arguments ) ) { UseShellExecute = false } );

                process!.WaitForExit();

                return process.ExitCode == 0;
            }

            void KillDocker()
            {
                ToolInvocationHelper.InvokeTool( context.Console, "docker", $"kill {containerName}" );
            }
        }
        finally
        {
            ToolInvocationHelper.InvokeTool( context.Console, "docker", $"image rm {imageName}" );
        }
    }

    private string GetArguments( DockerSettings settings, BuildContext context, string containerName, string imageName, DockerImage image )
    {
        var product = context.Product;
        var artifactsDirectory = Path.Combine( context.RepoDirectory, "artifacts" );

        var containerArtifactsDirectory = image.GetAbsolutePath( "src", product.ProductName, "artifacts" );

        Directory.CreateDirectory( artifactsDirectory );
        Directory.CreateDirectory( PathHelper.GetEngineeringDataDirectory() );

        // Add basic options.
        var argumentList = new List<string>()
        {
            "run",
            "-t", // Use TTY
            "--rm", // Remove after stop
            $"-v cache.nuget:{image.NuGetPackagesDirectory}",
            $"-v cache.build-artifacts:{image.DownloadedBuildArtifactsDirectory}",
            $"--mount type=bind,source={artifactsDirectory},target={containerArtifactsDirectory}",
            $"--mount type=bind,source={PathHelper.GetEngineeringDataDirectory()},target={image.EngineeringDataDirectory}",
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
        argumentList.Add( this.GetCommand( settings, image ) );

        var arguments = string.Join( " ", argumentList );

        return arguments;
    }

    protected abstract string GetCommand( DockerSettings settings, DockerImage image );

    protected virtual string[] Options => [];
}