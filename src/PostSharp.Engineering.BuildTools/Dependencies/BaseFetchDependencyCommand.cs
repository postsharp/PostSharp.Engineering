// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    /// <summary>
    /// Fetches the artifacts of the build dependencies.
    /// </summary>
    public abstract class BaseFetchDependencyCommand : BaseCommand<FetchDependenciesCommandSettings>
    {
        protected abstract bool Update { get; }

        protected override bool ExecuteCore( BuildContext context, FetchDependenciesCommandSettings settings )
        {
            context.Console.WriteHeading( "Fetching build artifacts" );

            if ( !settings.TryGetBuildConfiguration( context, out var configuration ) )
            {
                return false;
            }

            if ( !DependenciesOverrideFile.TryLoad( context, settings, configuration, out var dependenciesOverrideFile ) )
            {
                return false;
            }

            (TeamCityClient TeamCity, BuildConfiguration BuildConfiguration, ImmutableDictionary<string, string> ArtifactRules)? teamCityEmulation = null;

            if ( settings.SimulateContinuousIntegration )
            {
                if ( !DependenciesHelper.TryPrepareTeamCityEmulation( context, configuration, out teamCityEmulation ) )
                {
                    return false;
                }
            }

            if ( !DependenciesHelper.UpdateOrFetchDependencies( context, configuration, dependenciesOverrideFile, this.Update, teamCityEmulation ) )
            {
                return false;
            }

            if ( !dependenciesOverrideFile.TrySave( context, settings ) )
            {
                return false;
            }

            return true;
        }
    }
}