import jetbrains.buildServer.configs.kotlin.*
import jetbrains.buildServer.configs.kotlin.buildFeatures.Swabra
import jetbrains.buildServer.configs.kotlin.buildFeatures.sshAgent
import jetbrains.buildServer.configs.kotlin.buildFeatures.swabra
import jetbrains.buildServer.configs.kotlin.buildSteps.powerShell
import jetbrains.buildServer.configs.kotlin.triggers.vcs

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

version = "2022.10"

project {

    buildType(Metalama_Metalama20231_PostSharpEngineering_VersionBump)
    buildType(Metalama_Metalama20231_PostSharpEngineering_PublicDeployment)
    buildType(Metalama_Metalama20231_PostSharpEngineering_DebugBuild)
    buildType(Metalama_Metalama20231_PostSharpEngineering_PublicBuild)
    buildTypesOrder = arrayListOf(Metalama_Metalama20231_PostSharpEngineering_DebugBuild, Metalama_Metalama20231_PostSharpEngineering_PublicBuild, Metalama_Metalama20231_PostSharpEngineering_PublicDeployment, Metalama_Metalama20231_PostSharpEngineering_VersionBump)
}

object Metalama_Metalama20231_PostSharpEngineering_DebugBuild : BuildType({
    id("DebugBuild")
    name = "Build [Debug]"

    artifactRules = """
        +:artifacts/publish/public/**/*=>artifacts/publish/public
        +:artifacts/publish/private/**/*=>artifacts/publish/private
        +:artifacts/testResults/**/*=>artifacts/testResults
        +:artifacts/logs/**/*=>logs
        +:%system.teamcity.build.tempDir%/Metalama/AssemblyLocator/**/*=>logs
        +:%system.teamcity.build.tempDir%/Metalama/CompileTime/**/.completed=>logs
        +:%system.teamcity.build.tempDir%/Metalama/CompileTimeTroubleshooting/**/*=>logs
        +:%system.teamcity.build.tempDir%/Metalama/CrashReports/**/*=>logs
        +:%system.teamcity.build.tempDir%/Metalama/Extract/**/.completed=>logs
        +:%system.teamcity.build.tempDir%/Metalama/ExtractExceptions/**/*=>logs
        +:%system.teamcity.build.tempDir%/Metalama/Logs/**/*=>logs
    """.trimIndent()

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            name = "Kill background processes before cleanup"
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            scriptArgs = "tools kill"
        }
        powerShell {
            name = "Build [Debug]"
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            scriptArgs = "test --configuration Debug --buildNumber %build.number% --buildType %system.teamcity.buildType.id%"
        }
    }

    triggers {
        vcs {
            triggerRules = "-:comment=<<VERSION_BUMP>>|<<DEPENDENCIES_UPDATED>>:**"
            branchFilter = "+:<default>"
            watchChangesInDependencies = true
        }
    }

    features {
        swabra {
            lockingProcesses = Swabra.LockingProcessPolicy.KILL
            verbose = true
        }
    }

    requirements {
        equals("env.BuildAgentType", "caravela04cloud")
    }
})

object Metalama_Metalama20231_PostSharpEngineering_PublicBuild : BuildType({
    id("PublicBuild")
    name = "Build [Public]"

    artifactRules = """
        +:artifacts/publish/public/**/*=>artifacts/publish/public
        +:artifacts/publish/private/**/*=>artifacts/publish/private
        +:artifacts/testResults/**/*=>artifacts/testResults
        +:artifacts/logs/**/*=>logs
        +:%system.teamcity.build.tempDir%/Metalama/AssemblyLocator/**/*=>logs
        +:%system.teamcity.build.tempDir%/Metalama/CompileTime/**/.completed=>logs
        +:%system.teamcity.build.tempDir%/Metalama/CompileTimeTroubleshooting/**/*=>logs
        +:%system.teamcity.build.tempDir%/Metalama/CrashReports/**/*=>logs
        +:%system.teamcity.build.tempDir%/Metalama/Extract/**/.completed=>logs
        +:%system.teamcity.build.tempDir%/Metalama/ExtractExceptions/**/*=>logs
        +:%system.teamcity.build.tempDir%/Metalama/Logs/**/*=>logs
    """.trimIndent()

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            name = "Kill background processes before cleanup"
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            scriptArgs = "tools kill"
        }
        powerShell {
            name = "Build [Public]"
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            scriptArgs = "test --configuration Public --buildNumber %build.number% --buildType %system.teamcity.buildType.id%"
        }
    }

    features {
        swabra {
            lockingProcesses = Swabra.LockingProcessPolicy.KILL
            verbose = true
        }
    }

    requirements {
        equals("env.BuildAgentType", "caravela04cloud")
    }
})

object Metalama_Metalama20231_PostSharpEngineering_PublicDeployment : BuildType({
    id("PublicDeployment")
    name = "Deploy [Public]"

    type = BuildTypeSettings.Type.DEPLOYMENT

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            name = "Deploy [Public]"
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            scriptArgs = "publish --configuration Public"
        }
    }

    features {
        swabra {
            lockingProcesses = Swabra.LockingProcessPolicy.KILL
            verbose = true
        }
        sshAgent {
            teamcitySshKey = "PostSharp.Engineering"
        }
    }

    dependencies {
        dependency(Metalama_Metalama20231_PostSharpEngineering_PublicBuild) {
            snapshot {
                onDependencyFailure = FailureAction.FAIL_TO_START
            }

            artifacts {
                cleanDestination = true
                artifactRules = """
                    +:artifacts/publish/public/**/*=>artifacts/publish/public
                    +:artifacts/publish/private/**/*=>artifacts/publish/private
                    +:artifacts/testResults/**/*=>artifacts/testResults
                """.trimIndent()
            }
        }
    }

    requirements {
        equals("env.BuildAgentType", "caravela04cloud")
    }
})

object Metalama_Metalama20231_PostSharpEngineering_VersionBump : BuildType({
    id("VersionBump")
    name = "Version Bump"

    type = BuildTypeSettings.Type.DEPLOYMENT

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            name = "Version Bump"
            scriptMode = file {
                path = "Build.ps1"
            }
            noProfile = false
            scriptArgs = "bump"
        }
    }

    features {
        swabra {
            lockingProcesses = Swabra.LockingProcessPolicy.KILL
            verbose = true
        }
        sshAgent {
            teamcitySshKey = "PostSharp.Engineering"
        }
    }

    requirements {
        equals("env.BuildAgentType", "caravela04cloud")
    }
})
