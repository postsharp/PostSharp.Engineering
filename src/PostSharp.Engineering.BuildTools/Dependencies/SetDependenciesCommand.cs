// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using System;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    /// <summary>
    /// Sets the source of a dependency to <see cref="DependencySourceKind.Local"/>, <see cref="DependencySourceKind.BuildServer"/>
    /// or <see cref="DependencySourceKind.Feed"/>.
    /// </summary>
    public class SetDependenciesCommand : ConfigureDependenciesCommand<SetDependenciesCommandSettings>
    {
        protected override bool ConfigureDependency(
            BuildContext context,
            DependenciesOverrideFile dependenciesOverrideFile,
            DependencyDefinition dependencyDefinition,
            SetDependenciesCommandSettings settings )
        {
            DependencySource dependencySource;
            
            switch ( settings.Source )
            {
                case DependencySourceKind.Local:
                    dependencySource = DependencySource.CreateLocal( DependencyConfigurationOrigin.Override );

                    break;

                case DependencySourceKind.Feed:
                    dependencySource = DependencySource.CreateFeed( null, DependencyConfigurationOrigin.Override );

                    break;

                case DependencySourceKind.BuildServer:
                    ICiBuildSpec buildSpec;

                    if ( settings.Branch != null )
                    {
                        buildSpec = new CiLatestBuildOfBranch( settings.Branch );
                    }
                    else if ( settings.BuildNumber != null )
                    {
                        buildSpec = new CiBuildId( settings.BuildNumber.Value, settings.CiBuildTypeId );
                    }
                    else
                    {
                        context.Console.WriteError( "Either the --branch or --buildNumber parameter should be specified." );

                        return false;
                    }

                    dependencySource = DependencySource.CreateBuildServerSource(
                        buildSpec,
                        DependencyConfigurationOrigin.Override );

                    break;

                default:
                    throw new InvalidOperationException();
            }

            dependenciesOverrideFile.Dependencies[dependencyDefinition.Name] = dependencySource;

            return true;
        }
    }
}