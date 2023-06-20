// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using System;
using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration.Model
{
    internal record TeamCitySnapshotDependency( string ObjectId, bool IsAbsoluteId, string? ArtifactsRules = null );

    internal class TeamCityBuildConfiguration
    {
        public Product Product { get; }

        public string ObjectName { get; }

        public string Name { get; }

        public bool RequiresClearCache { get; init; }

        public string BuildArguments { get; }

        public string BuildAgentType { get; }

        public bool IsDeployment { get; init; }

        public bool IsSshAgentRequired { get; set; }

        public string? ArtifactRules { get; init; }

        public string[]? AdditionalArtifactRules { get; init; }

        public IBuildTrigger[]? BuildTriggers { get; init; }

        public TeamCitySnapshotDependency[]? Dependencies { get; init; }

        public bool RequiresUpstreamCheck { get; init; }
        
        public TimeSpan? MaxBuildDuration { get; init; }

        public TeamCityBuildConfiguration( Product product, string objectName, string name, string buildArguments, string buildAgentType )
        {
            this.Product = product;
            this.ObjectName = objectName;
            this.Name = name;
            this.BuildArguments = buildArguments;
            this.BuildAgentType = buildAgentType;
        }

        public void GenerateTeamcityCode( TextWriter writer )
        {
            var buildTimeOutThreshold = this.MaxBuildDuration ?? TimeSpan.FromMinutes( 5 );

            var buildTimeOutBase = this.MaxBuildDuration != null
                ? "value()"
                : @"build {
                buildRule = lastSuccessful()
            }";
            
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

            writer.WriteLine(
                $@"    vcs {{
        root(DslContext.settingsRoot)" );

            foreach ( var sourceDependency in this.Product.SourceDependencies )
            {
                writer.WriteLine(
                    $@"        root(AbsoluteId(""{sourceDependency.CiConfiguration.ProjectId}""), ""+:. => {this.Product.SourceDependenciesDirectory}/{sourceDependency.Name}"")" );
            }

            writer.WriteLine(
                $@"    }}

    steps {{" );

            if ( this.RequiresClearCache )
            {
                writer.WriteLine(
                    $@"        // Step to kill all dotnet or VBCSCompiler processes that might be locking files we delete in during cleanup.
        powerShell {{
            name = ""Kill background processes before cleanup""
            scriptMode = file {{
                path = ""Build.ps1""
            }}
            noProfile = false
            param(""jetbrains_powershell_scriptArguments"", ""tools kill"")
        }}" );
            }

            if ( this.RequiresUpstreamCheck )
            {
                writer.WriteLine(
                    $@"        powerShell {{
            name = ""Check pending upstream changes""
            scriptMode = file {{
                path = ""Build.ps1""
            }}
            noProfile = false
            param(""jetbrains_powershell_scriptArguments"", ""tools git check-upstream"")
        }}" );
            }
            
            writer.WriteLine(
                $@"        powerShell {{
            name = ""{this.Name}""
            scriptMode = file {{
                path = ""Build.ps1""
            }}
            noProfile = false
            param(""jetbrains_powershell_scriptArguments"", ""{this.BuildArguments}"")
        }}
    }}

    failureConditions {{
        failOnMetricChange {{
            metric = BuildFailureOnMetric.MetricType.BUILD_DURATION
            threshold = {buildTimeOutThreshold.TotalSeconds}
            units = BuildFailureOnMetric.MetricUnit.DEFAULT_UNIT
            comparison = BuildFailureOnMetric.MetricComparison.MORE
            compareTo = {buildTimeOutBase}
            stopBuildOnFailure = true
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

            if ( (this.RequiresUpstreamCheck || this.IsSshAgentRequired)
                 && this.Product.DependencyDefinition.VcsRepository.IsSshAgentRequired )
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
            var hasSnapshotDependencies = this.Dependencies is { Length: > 0 };

            if ( hasSnapshotDependencies )
            {
                writer.WriteLine(
                    $@"
    dependencies {{" );

                foreach ( var dependency in this.Dependencies!.OrderBy( d => d.ObjectId ) )
                {
                    var objectName = dependency.IsAbsoluteId ? @$"AbsoluteId(""{dependency.ObjectId}"")" : dependency.ObjectId;

                    writer.WriteLine(
                        $@"
        dependency({objectName}) {{
            snapshot {{
                     onDependencyFailure = FailureAction.FAIL_TO_START
            }}
" );

                    if ( dependency.ArtifactsRules != null )
                    {
                        writer.WriteLine(
                            $@"
            artifacts {{
                cleanDestination = true
                artifactRules = ""{dependency.ArtifactsRules}""
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