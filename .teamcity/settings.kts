// This file is automatically generated when you do `Build.ps1 prepare`.

import jetbrains.buildServer.configs.kotlin.v2019_2.*

// Both Swabra and swabra need to be imported
import jetbrains.buildServer.configs.kotlin.v2019_2.buildFeatures.sshAgent
import jetbrains.buildServer.configs.kotlin.v2019_2.buildFeatures.Swabra
import jetbrains.buildServer.configs.kotlin.v2019_2.buildFeatures.swabra
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.powerShell
import jetbrains.buildServer.configs.kotlin.v2019_2.triggers.*

version = "2021.2"

project {

   buildType(DebugBuild)
   buildType(ReleaseBuild)
   buildType(PublicBuild)
   buildType(PublicDeployment)
   buildType(VersionBump)
   buildTypesOrder = arrayListOf(DebugBuild,ReleaseBuild,PublicBuild,PublicDeployment,VersionBump)
}

object DebugBuild : BuildType({

    name = "Build [Debug]"

    artifactRules = "+:artifacts/publish/public/**/*=>artifacts/publish/public\n+:artifacts/publish/private/**/*=>artifacts/publish/private"

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            param("jetbrains_powershell_scriptArguments", "test --configuration Debug --buildNumber %build.number%")
        }
    }

    requirements {
        equals("env.BuildAgentType", "caravela02")
    }

    features {
        swabra {
            lockingProcesses = Swabra.LockingProcessPolicy.KILL
            verbose = true
        }
    }

    triggers {

        vcs {
            watchChangesInDependencies = true
            branchFilter = "+:<default>"
            // Build will not trigger automatically if the commit message contains comment value.
            triggerRules = "-:comment=<<VERSION_BUMP>>:**"
        }        

    }

})

object ReleaseBuild : BuildType({

    name = "Build [Release]"

    artifactRules = "+:artifacts/publish/public/**/*=>artifacts/publish/public\n+:artifacts/publish/private/**/*=>artifacts/publish/private"

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            param("jetbrains_powershell_scriptArguments", "test --configuration Release --buildNumber %build.number%")
        }
    }

    requirements {
        equals("env.BuildAgentType", "caravela02")
    }

    features {
        swabra {
            lockingProcesses = Swabra.LockingProcessPolicy.KILL
            verbose = true
        }
    }

})

object PublicBuild : BuildType({

    name = "Build [Public]"

    artifactRules = "+:artifacts/publish/public/**/*=>artifacts/publish/public\n+:artifacts/publish/private/**/*=>artifacts/publish/private"

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            param("jetbrains_powershell_scriptArguments", "test --configuration Public --buildNumber %build.number%")
        }
    }

    requirements {
        equals("env.BuildAgentType", "caravela02")
    }

    features {
        swabra {
            lockingProcesses = Swabra.LockingProcessPolicy.KILL
            verbose = true
        }
    }

})

object PublicDeployment : BuildType({

    name = "Deploy [Public]"

    type = Type.DEPLOYMENT

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            param("jetbrains_powershell_scriptArguments", "publish --configuration Public")
        }
    }

    requirements {
        equals("env.BuildAgentType", "caravela02")
    }

    features {
        swabra {
            lockingProcesses = Swabra.LockingProcessPolicy.KILL
            verbose = true
        }
        sshAgent {
            // By convention, the SSH key name is always PostSharp.Engineering for all repositories using SSH to connect.
            teamcitySshKey = "PostSharp.Engineering"
        }
    }

  dependencies {

        dependency(PublicBuild) {
            snapshot {
                onDependencyFailure = FailureAction.FAIL_TO_START
            }

            artifacts {
                cleanDestination = true
                artifactRules = "+:artifacts/publish/public/**/*=>artifacts/publish/public\n+:artifacts/publish/private/**/*=>artifacts/publish/private"
            }
        }

     }

})

object VersionBump : BuildType({

    name = "Version Bump"

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            param("jetbrains_powershell_scriptArguments", "--version bump")
        }
    }

    requirements {
        equals("env.BuildAgentType", "caravela02")
    }

    features {
        swabra {
            lockingProcesses = Swabra.LockingProcessPolicy.KILL
            verbose = true
        }
    }

    triggers {

        finishBuildTrigger {
            buildType = "postsharp_PostSharpEngineering_PublicDeployment"
            // Only successful deployment will trigger the version bump.
            successfulOnly = true
            branchFilter = "+:<default>"
        }        

    }

})

