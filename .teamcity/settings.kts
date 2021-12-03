import jetbrains.buildServer.configs.kotlin.v2019_2.*
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.powerShell
import jetbrains.buildServer.configs.kotlin.v2019_2.triggers.vcs

version = "2019.2"

project {

    buildType(DebugBuild)
    buildType(DebugInternalPublish)
    buildType(ReleaseBuild)
    buildType(ReleaseInternalPublish)
    buildType(ReleasePublicRelease)
}

// Debug build (a numbered build)
object DebugBuild : BuildType({
    name = "Build [Debug]"

    artifactRules = "+:artifacts/publish/**/*=>artifacts/publish"

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
        equals("env.BuildAgentType", "caravela02")
    }
})

// Release build (with unsuffixed version number)
object ReleaseBuild : BuildType({
    name = "Build [Release]"

    artifactRules = "+:artifacts/publish/**/*=>artifacts/publish"

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
        equals("env.BuildAgentType", "caravela02")
    }
})

// Publish the internal build
object DebugInternalPublish : BuildType({
    name = "Publish Internally [Debug]"
    type = BuildTypeSettings.Type.DEPLOYMENT

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            param("jetbrains_powershell_scriptArguments", "publish")
        }
    }

    dependencies {
        dependency(DebugBuild) {
            snapshot {
            }

            artifacts {
                cleanDestination = true
                artifactRules = "+:artifacts/publish/**/*=>artifacts/publish"
            }
        }
    }
    
    
    requirements {
        equals("env.BuildAgentType", "caravela02")
    }
})

// Publish the release build internally
object ReleaseInternalPublish : BuildType({
    name = "Publish Internally [Release]"
    type = BuildTypeSettings.Type.DEPLOYMENT

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            param("jetbrains_powershell_scriptArguments", "publish")
        }
    }

    dependencies {
        dependency(ReleaseBuild) {
            snapshot {
            }

            artifacts {
                cleanDestination = true
                artifactRules = "+:artifacts/publish/**/*=>artifacts/publish"
            }
        }
    }
    
    
    requirements {
        equals("env.BuildAgentType", "caravela02")
    }
})


// Publish the release build to public feeds
object ReleasePublicRelease : BuildType({
    name = "Publish Externally [Release]"
    type = BuildTypeSettings.Type.DEPLOYMENT

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
        dependency(ReleaseBuild) {
            snapshot {
            }

            artifacts {
                cleanDestination = true
                artifactRules = "+:artifacts/publish/**/*=>artifacts/publish"
            }
        }
    }
        
        
    
    
    requirements {
        equals("env.BuildAgentType", "caravela02")
    }

})
