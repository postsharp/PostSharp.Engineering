// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;

namespace PostSharp.Engineering.BuildTools.Dependencies;

/// <summary>
/// Updates configuration of all dependencies in the version file.
/// </summary>
public class UpdateDependencyCommand : BaseCommand<BaseDependenciesCommandSettings>
{
    protected override bool ExecuteCore( BuildContext context, BaseDependenciesCommandSettings settings )
    {
        context.Console.WriteHeading( "Updating dependencies" );

        if ( !settings.TryGetBuildConfiguration( context, out var configuration ) )
        {
            return false;
        }

        if ( !DependenciesOverrideFile.TryLoad( context, configuration, out var dependenciesOverrideFile ) )
        {
            return false;
        }

        if ( !FetchDependencyCommand.FetchDependencies( context, configuration, dependenciesOverrideFile, true ) )
        {
            return false;
        }

        if ( !dependenciesOverrideFile.TrySave( context ) )
        {
            return false;
        }

        return true;
    }

    public static bool UpdateDependency( BuildContext context, out DependenciesOverrideFile? newDependenciesOverrideFile )
    {
        var settings = new BaseDependenciesCommandSettings();
        
        newDependenciesOverrideFile = null;
        
        if ( !settings.TryGetBuildConfiguration( context, out var configuration ) )
        {
            return false;
        }

        if ( !DependenciesOverrideFile.TryLoad( context, configuration, out var dependenciesOverrideFile ) )
        {
            return false;
        }

        newDependenciesOverrideFile = dependenciesOverrideFile;

        if ( !FetchDependencyCommand.FetchDependencies( context, configuration, dependenciesOverrideFile, true ) )
        {
            return false;
        }

        if ( !dependenciesOverrideFile.TrySave( context ) )
        {
            return false;
        }

        return true;
    }
}