import jetbrains.buildServer.configs.kotlin.v2019_2.*
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.powerShell
import jetbrains.buildServer.configs.kotlin.v2019_2.triggers.vcs

version = "2019.2"

// All values that can differ between repos and branches should be here so the rest is easier to merge.
val buildAgentType = "caravela02"
val artifactsPath = "artifacts/publish"

project {
    buildType(DebugBuild)
    buildType(ReleaseBuild)
    buildType(PublicBuild)
    buildType(Deploy)
}

// Debug build (a numbered build)
object DebugBuild : BuildType({
    name = "Build [Debug]"

    artifactRules = "+:$artifactsPath/**/*=>$artifactsPath"

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            param("jetbrains_powershell_scriptArguments", "test --numbered %build.number%")
        }
    }

    triggers {
        vcs {
        }
    }

    requirements {
        equals("env.BuildAgentType", buildAgentType)
    }
})

// Release build (with unsuffixed version number)
object ReleaseBuild : BuildType({
    name = "Build [Release]"

    artifactRules = "+:$artifactsPath/**/*=>$artifactsPath"

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            param("jetbrains_powershell_scriptArguments", "test  --numbered %build.number% --configuration Release --sign")
        }
    }

    triggers {
        vcs {
        }
    }

    requirements {
        equals("env.BuildAgentType", buildAgentType)
    }
})

// Public build (a release build with unsuffixed version number)
object PublicBuild : BuildType({
    name = "Build [Public]"

    artifactRules = "+:$artifactsPath/**/*=>$artifactsPath"

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            param("jetbrains_powershell_scriptArguments", "test --public --configuration Release --sign")
        }
    }

    triggers {
        vcs {
        }
    }
    
   requirements {
        equals("env.BuildAgentType", buildAgentType)
    }
})

// Publish the release build to public feeds
object Deploy : BuildType({
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
            param("jetbrains_powershell_scriptArguments", "publish --public")
        }
    }
    
  dependencies {
        dependency(PublicBuild) {
            snapshot {
            }

            artifacts {
                cleanDestination = true
                artifactRules = "+:$artifactsPath/**/*=>$artifactsPath"
            }
        }
    }
    
   requirements {
        equals("env.BuildAgentType", buildAgentType)
    }
})
