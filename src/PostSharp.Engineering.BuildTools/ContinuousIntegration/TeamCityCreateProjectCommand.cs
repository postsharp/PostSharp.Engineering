﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public class TeamCityCreateProjectCommand : BaseCommand<TeamCityCreateProjectCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, TeamCityCreateProjectCommandSettings settings )
    {
        context.Console.WriteHeading( $"Creating project {settings.Name}" );
        
        if ( !TeamCityHelper.TryCreateProject( context, settings.Name, settings.Id, settings.ParentId, settings.VcsRootId ) )
        {
            return false;
        }

        context.Console.WriteSuccess( $"Project '{settings.Name}' created successfully." );

        return true;
    }
}