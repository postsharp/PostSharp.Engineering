// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PostSharp.Engineering.BuildTools.Docker;

public abstract class DockerRunCommand : BaseCommand<BuildSettings>
{
    protected override bool ExecuteCore( BuildContext context, BuildSettings settings )
    {
        if ( !DockerPrepareCommand.TryPrepare( context, settings, out var imageName ) )
        {
            return false;
        }

        context.Console.WriteHeading( $"Building {context.Product.ProductName} from docker." );

        var argumentList = new List<string>()
        {
            "run",
            "-t",
            "-e TEAMCITY_CLOUD_TOKEN=\"%TEAMCITY_CLOUD_TOKEN%\"",
            "-v cache.nuget:/root/.nuget/packages",
            "-v cache.build-artifacts:/tmp/.build-artifacts/"
        };

        argumentList.AddRange( this.Options );
        argumentList.Add( imageName );
        argumentList.Add( this.GetCommand( settings ) );

        var arguments = Environment.ExpandEnvironmentVariables( string.Join( " ", argumentList ) );

        Console.WriteLine( arguments );

        var process = Process.Start( new ProcessStartInfo( "docker", arguments ) { UseShellExecute = false } );
        process!.WaitForExit();

        return process.ExitCode == 0;
    }

    protected abstract string GetCommand( BuildSettings settings );

    protected virtual string[] Options => [];
}