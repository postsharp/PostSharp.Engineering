// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using Spectre.Console;

namespace PostSharp.Engineering.BuildTools.Docker;

public class DockerListImagesCommand : BaseCommand<CommonCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, CommonCommandSettings settings )
    {
        Table table = new();
        table.AddColumn( "Name" );
        table.AddColumn( "Uri" );

        foreach ( var image in DockerImages.All )
        {
            table.AddRow( image.Name, image.Uri );
        }

        context.Console.Out.Write( table );

        return true;
    }
}