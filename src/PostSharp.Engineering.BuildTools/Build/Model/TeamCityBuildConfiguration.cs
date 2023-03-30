// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Build.Model
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

        public string? ArtifactRules { get; init; }

        public string[]? AdditionalArtifactRules { get; init; }

        public IBuildTrigger[]? BuildTriggers { get; init; }

        public TeamCitySnapshotDependency[]? Dependencies { get; init; }

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
                    $@"
  root(AbsoluteId(""{sourceDependency.VcsConfigName}""), ""+:. => {this.Product.SourceDependenciesDirectory}/{sourceDependency.Name}"")" );
            }

            writer.WriteLine(
                    $@"
        }}

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

    requirements {{
        equals(""env.BuildAgentType"", ""{this.BuildAgentType}"")
    }}

    features {{
        swabra {{
            lockingProcesses = Swabra.LockingProcessPolicy.KILL
            verbose = true
        }}" );

                var productVcsProvider = this.Product.VcsProvider;

                // The SSH agent is added only for the Deployment and only if TeamCity uses SSH for Git operations over the product VCS repository.
                if ( productVcsProvider != null && this.IsDeployment && productVcsProvider.SshAgentRequired )
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