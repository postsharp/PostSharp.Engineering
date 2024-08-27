﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using Microsoft.Build.Locator;
using Spectre.Console;

namespace PostSharp.Engineering.BuildTools.Build;

[UsedImplicitly]
public class ListMSBuildCommand : BaseCommand<CommonCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, CommonCommandSettings settings )
    {
        var table = new Table();
        table.AddColumns( "Name", "Version", "Path", "Type" );

        // List instances discovered by MSBuildLocator.
        foreach ( var instance in MSBuildLocator.QueryVisualStudioInstances() )
        {
            table.AddRow( instance.Name, instance.Version.ToString(), instance.MSBuildPath, instance.DiscoveryType.ToString() );
        }

        // List instances discovered by Visual Studio installer.
        foreach ( var instance in MSBuildHelper.GetVisualStudioInstances() )
        {
            table.AddRow( instance.Name, instance.Version.ToString(), instance.Path, "VS" );
        }

        context.Console.Write( table );

        context.Console.WriteMessage( $"MSBuildHelper.FindLatestMSBuildExe returns: {MSBuildHelper.FindLatestMSBuildExe() ?? "<null>"}" );

        return true;
    }
}