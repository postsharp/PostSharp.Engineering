// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public class TeamCityCreateProjectCommand : BaseCommand<TeamCityCreateProjectCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, TeamCityCreateProjectCommandSettings settings )
    {
        context.Console.WriteHeading( $"Creating project {settings.Name}" );
        
        if ( !TeamCityHelper.TryConnectTeamCity( context, out var tc ) )
        {
            return false;
        }

        context.Console.WriteMessage( $"Creating project \"{settings.Name}\", ID \"{settings.Id}\", parent project ID \"{settings.ParentId}\"." );
        
        if ( !tc.TryCreateProject( context.Console, settings.Name, settings.Id, settings.ParentId ) )
        {
            return false;
        }

        if ( settings.VcsRootId != null )
        {
            context.Console.WriteMessage( $"Setting versioned settings for project \"{settings.Name}\" (\"{settings.Id}\") using VCS root ID \"{settings.VcsRootId}\"." );
            
            if ( !tc.TrySetProjectVersionedSettings( context.Console, settings.Id, settings.VcsRootId ) )
            {
                return false;
            }
        }

        context.Console.WriteSuccess( $"Project \"{settings.Name}\" created successfully." );

        return true;
    }
}