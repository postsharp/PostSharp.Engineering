﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;
using System;
using System.Collections.Generic;

namespace PostSharp.Engineering.BuildTools.Search;

public class UpdateSearchProductExtension : ProductExtension
{
    public string TypesenseUri { get; }

    public string Source { get; }

    public string SourceUrl { get; }

    public bool IgnoreTls { get; }

    public BuildConfiguration[] BuildConfigurations { get; }

    public TimeSpan TimeOutThreshold { get; }

    public UpdateSearchProductExtension(
        string typesenseUri,
        string source,
        string sourceUrl,
        bool ignoreTls = false,
        BuildConfiguration[]? buildConfigurations = null,
        TimeSpan? timeOutThreshold = null )
    {
        this.TypesenseUri = typesenseUri;
        this.Source = source;
        this.SourceUrl = sourceUrl;
        this.IgnoreTls = ignoreTls;
        this.BuildConfigurations = buildConfigurations ?? new[] { BuildConfiguration.Public };
        this.TimeOutThreshold = timeOutThreshold ?? TimeSpan.FromMinutes( 5 );
    }

    private string GetArguments()
    {
        var arguments = new List<string>();
        arguments.Add( "tools" );
        arguments.Add( "search" );
        arguments.Add( "update" );
        arguments.Add( this.TypesenseUri );
        arguments.Add( this.Source );
        arguments.Add( this.SourceUrl );

        if ( this.IgnoreTls )
        {
            arguments.Add( "--ignore-tls" );
        }

        return string.Join( " ", arguments );
    }

    internal override bool AddTeamcityBuildConfiguration( BuildContext context, List<TeamCityBuildConfiguration> teamCityBuildConfigurations )
    {
        foreach ( var configuration in this.BuildConfigurations )
        {
            var configurationInfo = context.Product.Configurations[configuration];

            var name = $"Update Search [{configuration}]";

            if ( !configurationInfo.ExportsToTeamCityBuild )
            {
                context.Console.WriteError(
                    $"Cannot crate '{name}' TeamCity Build configuration because the '{configuration}' build configuration is not exportable to TeamCity." );

                return false;
            }

            if ( !configurationInfo.ExportsToTeamCityDeploy )
            {
                context.Console.WriteError(
                    $"Cannot crate '{name}' TeamCity Build configuration because the '{configuration}' build configuration doesn't export deployment TeamCity build configuration to TeamCity." );

                return false;
            }
            
            var isRepoRemoteSsh = context.Product.DependencyDefinition.VcsRepository.IsSshAgentRequired;

            var teamCityUpdateSearchConfiguration = new TeamCityBuildConfiguration(
                $"{configuration}UpdateSearch",
                name,
                this.GetArguments(),
                context.Product.DependencyDefinition.CiConfiguration.BuildAgentType,
                isRepoRemoteSsh )
            {
                IsDeployment = true,
                SnapshotDependencies = new[] { new TeamCitySnapshotDependency( $"{configuration}Deployment", false ) },
                BuildTimeOutThreshold = this.TimeOutThreshold
            };

            teamCityBuildConfigurations.Add( teamCityUpdateSearchConfiguration );

            if ( configurationInfo.ExportsToTeamCityDeployWithoutDependencies )
            {
                var teamCityUpdateSearchWithoutDependenciesConfiguration = new TeamCityBuildConfiguration(
                    $"{configuration}UpdateSearchNoDependency",
                    $"Standalone {name}",
                    this.GetArguments(),
                    context.Product.DependencyDefinition.CiConfiguration.BuildAgentType,
                    isRepoRemoteSsh )
                {
                    IsDeployment = true, BuildTimeOutThreshold = this.TimeOutThreshold
                };

                teamCityBuildConfigurations.Add( teamCityUpdateSearchWithoutDependenciesConfiguration );
            }
        }

        return true;
    }
}