// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
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

        public string BuildArguments { get; }

        public string BuildAgentType { get; }
        
        public bool IsRepoRemoteSsh { get; }

        public bool IsDeployment { get; init; }
        
        public bool IsClearCacheRequired { get; init; }
        
        public bool IsGitAuthenticationRequired { get; init; }

        public string? ArtifactRules { get; init; }

        public string[]? AdditionalArtifactRules { get; init; }

        public IBuildTrigger[]? BuildTriggers { get; init; }
        
        public TeamCitySnapshotDependency[]? SnapshotDependencies { get; init; }
        
        public TeamCitySourceDependency[]? SourceDependencies { get; init; }

        public bool RequiresUpstreamCheck { get; init; }
        
        public TimeSpan BuildTimeOutThreshold { get; init; }

        public TeamCityBuildConfiguration( string objectName, string name, string buildArguments, string buildAgentType, bool isRepoRemoteSsh )
        {
            this.ObjectName = objectName;
            this.Name = name;
            this.BuildArguments = buildArguments;
            this.BuildAgentType = buildAgentType;
            this.IsRepoRemoteSsh = isRepoRemoteSsh;
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

            var parameters = new List<string>
            {
                @"        text(""BuildArguments"", """", label = ""Build Arguments"", description = ""Arguments to append to the engineering command."", allowEmpty = true)"
            };

            if ( this.RequiresUpstreamCheck )
            {
                parameters.Add(
                    @"        text(""UpstreamCheckArguments"", """", label = ""Upstream Check Arguments"", description = ""Arguments to append to the upstream check command."", allowEmpty = true)" );
            }

            parameters.Add(
                @$"        text(""TimeOut"", ""{(int) this.BuildTimeOutThreshold.TotalSeconds}"", label = ""Time-Out Threshold"", description = ""Seconds after the duration of the last successful build."",
              regex = """"""\d+"""""", validationMessage = ""The timeout has to be an integer number."")" );
            
            writer.WriteLine(
                $@"    params {{
{string.Join( Environment.NewLine, parameters )}
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

            writer.WriteLine(
                $@"    }}

    steps {{" );

            if ( this.IsClearCacheRequired )
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
            param(""jetbrains_powershell_scriptArguments"", ""tools git check-upstream %UpstreamCheckArguments%"")
        }}" );
            }
            
            writer.WriteLine(
                $@"        powerShell {{
            name = ""{this.Name}""
            scriptMode = file {{
                path = ""Build.ps1""
            }}
            noProfile = false
            param(""jetbrains_powershell_scriptArguments"", ""{this.BuildArguments} %BuildArguments%"")
        }}
    }}

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

            if ( (this.RequiresUpstreamCheck || this.IsGitAuthenticationRequired) && this.IsRepoRemoteSsh )
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