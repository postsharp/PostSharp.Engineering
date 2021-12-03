import jetbrains.buildServer.configs.kotlin.v2019_2.*
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.powerShell
import jetbrains.buildServer.configs.kotlin.v2019_2.triggers.vcs

/*
The settings script is an entry point for defining a TeamCity
project hierarchy. The script should contain a single call to the
project() function with a Project instance or an init function as
an argument.

VcsRoots, BuildTypes, Templates, and subprojects can be
registered inside the project using the vcsRoot(), buildType(),
template(), and subProject() methods respectively.

To debug settings scripts in command-line, run the

    mvnDebug org.jetbrains.teamcity:teamcity-configs-maven-plugin:generate

command and attach your debugger to the port 8000.

To debug in IntelliJ Idea, open the 'Maven Projects' tool window (View
-> Tool Windows -> Maven Projects), find the generate task node
(Plugins -> teamcity-configs -> teamcity-configs:generate), the
'Debug' option is available in the context menu for the task.
*/

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
            param("jetbrains_powershell_scriptArguments", "build --numbered %build.number%")
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
            param("jetbrains_powershell_scriptArguments", "build --public --configuration Release --sign")
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
