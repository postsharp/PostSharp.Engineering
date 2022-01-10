using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using System;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    public class SetDependenciesCommand : ConfigureDependenciesCommand<SetDependenciesCommandSettings>
    {
        protected override bool ConfigureDependency(
            BuildContext context,
            VersionsOverrideFile versionsOverrideFile,
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
                    if ( settings.BuildNumber != null )
                    {
                        var ciBuildTypeId = settings.CiBuildTypeId ?? dependencyDefinition.DefaultCiBuildTypeId;

                        dependencySource = DependencySource.CreateBuildServerSource(
                            settings.BuildNumber.Value,
                            ciBuildTypeId,
                            null,
                            DependencyConfigurationOrigin.Override );
                    }
                    else if ( settings.Branch != null )
                    {
                        var branch = settings.Branch;
                        var ciBuildTypeId = settings.CiBuildTypeId ?? dependencyDefinition.DefaultCiBuildTypeId;
                        dependencySource = DependencySource.CreateBuildServerSource( branch, ciBuildTypeId, DependencyConfigurationOrigin.Override );
                    }
                    else
                    {
                        var branch = dependencyDefinition.DefaultBranch;
                        var ciBuildTypeId = settings.CiBuildTypeId ?? dependencyDefinition.DefaultCiBuildTypeId;
                        dependencySource = DependencySource.CreateBuildServerSource( branch, ciBuildTypeId, DependencyConfigurationOrigin.Override );
                    }

                    break;

                default:
                    throw new InvalidOperationException();
            }

            versionsOverrideFile.Dependencies[dependencyDefinition.Name] = dependencySource;

            return true;
        }
    }
}