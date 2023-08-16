// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model.Arguments;
using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model.BuildSteps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration.Model
{
    internal record TeamCitySnapshotDependency( string ObjectId, bool IsAbsoluteId, string? ArtifactRules = null );

    internal record TeamCitySourceDependency( string ObjectId, bool IsAbsoluteId, string ArtifactRules );

    internal class TeamCityBuildConfiguration
    {
        public string ObjectName { get; }

        public string Name { get; }

        public string BuildAgentType { get; }

        public TeamCityBuildStep[]? BuildSteps { get; init; }
        
        public bool IsDeployment { get; init; }
        
        public bool IsSshAgentRequired { get; init; }

        public string? ArtifactRules { get; init; }

        public string[]? AdditionalArtifactRules { get; init; }

        public IBuildTrigger[]? BuildTriggers { get; init; }
        
        public TeamCitySnapshotDependency[]? SnapshotDependencies { get; init; }
        
        public TeamCitySourceDependency[]? SourceDependencies { get; init; }
        
        public TimeSpan BuildTimeOutThreshold { get; init; }

        public TeamCityBuildConfiguration( string objectName, string name, string buildAgentType )
        {
            this.ObjectName = objectName;
            this.Name = name;
            this.BuildAgentType = buildAgentType;
        }

        public void GenerateTeamcityCode( TextWriter writer )
        {
            writer.WriteLine(
                $@"object {this.ObjectName} : BuildType({{

    name = ""{this.Name}""
" );

            if ( this.IsDeployment )
            {
                writer.WriteLine( "    type = Type.DEPLOYMENT" );
                writer.WriteLine();
            }

            if ( this.ArtifactRules != null )
            {
                if ( this.AdditionalArtifactRules != null )
                {
                    writer.WriteLine( $@"    artifactRules = ""{this.ArtifactRules}\n{string.Join( $@"\n", this.AdditionalArtifactRules )}""" );
                }
                else
                {
                    writer.WriteLine( $@"    artifactRules = ""{this.ArtifactRules}""" );
                }

                writer.WriteLine();
            }

            var timeOutParameter = new TeamCityTextBuildConfigurationParameter(
                "TimeOut",
                "Time-Out Threshold",
                "Seconds after the duration of the last successful build.",
                $"{(int) this.BuildTimeOutThreshold.TotalSeconds}" ) { Validation = (@"\d+", "The timeout has to be an integer number.") };

            var hasBuildSteps = this.BuildSteps is { Length: > 0 };
            
            var buildParameters = new List<TeamCityBuildConfigurationParameter>();

            if ( hasBuildSteps )
            {
                buildParameters.AddRange(
                    this.BuildSteps!.SelectMany( s => s.BuildConfigurationParameters ?? Enumerable.Empty<TeamCityBuildConfigurationParameter>() ) );
            }
            
            buildParameters.Add( timeOutParameter );

            writer.WriteLine(
                $@"    params {{
{string.Join( Environment.NewLine, buildParameters.Select( p => p.GenerateTeamCityCode() ) )}
    }}" );
            
            writer.WriteLine(
                $@"    vcs {{
        root(DslContext.settingsRoot)" );

            // Source dependencies.
            var hasSourceDependencies = this.SourceDependencies is { Length: > 0 };

            if ( hasSourceDependencies )
            {
                foreach ( var sourceDependency in this.SourceDependencies!.OrderBy( d => d.ObjectId ) )
                {
                    var objectName = sourceDependency.IsAbsoluteId ? @$"AbsoluteId(""{sourceDependency.ObjectId}"")" : sourceDependency.ObjectId;
                    
                    writer.WriteLine(
                        $@"        root({objectName}, ""{sourceDependency.ArtifactRules}"")" );
                }
            }

            writer.WriteLine( $@"    }}" );

            // Build steps.
            if ( hasBuildSteps )
            {
                writer.WriteLine(
                    $@"
    steps {{" );

                foreach ( var buildStep in this.BuildSteps! )
                {
                    writer.WriteLine( buildStep.GenerateTeamCityCode() );
                }

                writer.WriteLine( @"    }" );
            }

            writer.WriteLine(
                @$"
    failureConditions {{
        failOnMetricChange {{
            metric = BuildFailureOnMetric.MetricType.BUILD_DURATION
            units = BuildFailureOnMetric.MetricUnit.DEFAULT_UNIT
            comparison = BuildFailureOnMetric.MetricComparison.MORE
            compareTo = build {{
                buildRule = lastSuccessful()
            }}
            stopBuildOnFailure = true
            param(""metricThreshold"", ""%TimeOut%"")
        }}
    }}

    requirements {{
        equals(""env.BuildAgentType"", ""{this.BuildAgentType}"")
    }}

    features {{
        swabra {{
            lockingProcesses = Swabra.LockingProcessPolicy.KILL
            verbose = true
        }}" );

            if ( this.IsSshAgentRequired )
            {
                writer.WriteLine(
                    $@"        sshAgent {{
            // By convention, the SSH key name is always PostSharp.Engineering for all repositories using SSH to connect.
            teamcitySshKey = ""PostSharp.Engineering""
        }}" );
            }

            writer.WriteLine( $@"    }}" );

            // Triggers.
            if ( this.BuildTriggers is { Length: > 0 } )
            {
                writer.WriteLine(
                    @"
    triggers {" );

                foreach ( var trigger in this.BuildTriggers )
                {
                    trigger.GenerateTeamcityCode( writer );
                }

                writer.WriteLine(
                    @"
    }" );
            }

            // Dependencies
            var hasSnapshotDependencies = this.SnapshotDependencies is { Length: > 0 };

            if ( hasSnapshotDependencies )
            {
                writer.WriteLine(
                    $@"
    dependencies {{" );

                foreach ( var dependency in this.SnapshotDependencies!.OrderBy( d => d.ObjectId ) )
                {
                    var objectName = dependency.IsAbsoluteId ? @$"AbsoluteId(""{dependency.ObjectId}"")" : dependency.ObjectId;

                    writer.WriteLine(
                        $@"
        dependency({objectName}) {{
            snapshot {{
                     onDependencyFailure = FailureAction.FAIL_TO_START
            }}
" );

                    if ( dependency.ArtifactRules != null )
                    {
                        writer.WriteLine(
                            $@"
            artifacts {{
                cleanDestination = true
                artifactRules = ""{dependency.ArtifactRules}""
            }}" );
                    }

                    writer.WriteLine(
                        $@"
        }}" );
                }

                writer.WriteLine(
                    $@"
     }}" );
            }

            writer.WriteLine(
                $@"
}})" );
        }
    }
}