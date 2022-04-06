using System.IO;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    internal class TeamCityBuildConfiguration
    {
        public Product Product { get; }

        public string ObjectName { get; }

        public string Name { get; }

        public string BuildArguments { get; }

        public string BuildAgentType { get; }

        public bool IsDeployment { get; init; }
   
        public string? ArtifactRules { get; init; }

        public string[]? AdditionalArtifactRules { get; init; }

        public IBuildTrigger[]? BuildTriggers { get; init; }

        public string[]? SnapshotDependencyObjectNames { get; init; }

        public (string ObjectName, string ArtifactRules)[]? ArtifactDependencies { get; init; }

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
        root(DslContext.settingsRoot)
    }}

    steps {{
        powerShell {{
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
            
            // The SSH agent is added only for the Deployment and only if TeamCity needs it to use Git operations on the VCS repository.
            if ( productVcsProvider != null && this.IsDeployment && productVcsProvider.SshAgentRequired )
            {
                writer.WriteLine(
                    $@"        sshAgent {{
            // By convention, the SSH key name is the same as the product name.
            teamcitySshKey = ""{this.Product.ProductName}""
        }}" );
            }

            writer.WriteLine(
                $@"    }}" );

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
            var hasSnapshotDependencies = this.SnapshotDependencyObjectNames is { Length: > 0 };
            var hasArtifactDependencies = this.ArtifactDependencies is { Length: > 0 };
            var hasDependencies = hasSnapshotDependencies || hasArtifactDependencies;

            if ( hasDependencies )
            {
                writer.WriteLine(
                    $@"
  dependencies {{" );
            }

            // Snapshot dependencies.
            if ( hasSnapshotDependencies )
            {
                foreach ( var dependency in this.SnapshotDependencyObjectNames! )
                {
                    writer.WriteLine(
                        $@"
        snapshot(AbsoluteId(""{dependency}"")) {{
                     onDependencyFailure = FailureAction.FAIL_TO_START
                }}" );
                }
            }

            // Artifact dependencies
            if ( hasArtifactDependencies )
            {
                foreach ( var dependency in this.ArtifactDependencies! )
                {
                    writer.WriteLine(
                        $@"
        dependency({dependency.ObjectName}) {{
            snapshot {{
                onDependencyFailure = FailureAction.FAIL_TO_START
            }}

            artifacts {{
                cleanDestination = true
                artifactRules = ""{dependency.ArtifactRules}""
            }}
        }}" );
                }
            }

            if ( hasDependencies )
            {
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