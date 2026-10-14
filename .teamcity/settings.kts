import jetbrains.buildServer.configs.kotlin.*
import jetbrains.buildServer.configs.kotlin.buildSteps.script
import jetbrains.buildServer.configs.kotlin.triggers.vcs
import jetbrains.buildServer.configs.kotlin.triggers.finishBuildTrigger

version = "2026.2"

project {
    // The root project's dynamic branch parameter is undefined in TeamCity's DSL compiler job.
    params { param("env.branch_name", "master") }
    buildType(ApiCi)
    buildType(ApiDeploy)
}

object ApiCi : BuildType({
    name = "api-ci"
    description = "Test and publish the .NET API as a self-contained Windows IIS artifact."
    artifactRules = """
        output/teamcity-api/api.zip => api
        output/teamcity-api/api-manifest.json => api
        output/teamcity-api/test-results/** => test-results
    """.trimIndent()
    publishArtifacts = PublishMode.SUCCESSFUL
    vcs {
        root(DslContext.settingsRoot)
        checkoutMode = CheckoutMode.ON_AGENT
        cleanCheckout = true
        branchFilter = "+:<default>"
    }
    steps {
        script {
            name = "Test and package API"
            scriptContent = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/ci/build-api.ps1"
        }
    }
    triggers {
        vcs {
            branchFilter = "+:<default>"
            triggerRules = """
                +:api/**
                +:scripts/ci/**
                +:.teamcity/**
            """.trimIndent()
        }
    }
    requirements { contains("teamcity.agent.jvm.os.name", "Windows") }
})

object ApiDeploy : BuildType({
    name = "api-deploy"
    description = "Deploy the matching successful CI artifact to octopus-dash-api IIS and check database health/authentication."
    type = BuildTypeSettings.Type.DEPLOYMENT
    enablePersonalBuilds = false
    maxRunningBuilds = 1
    vcs {
        root(DslContext.settingsRoot)
        checkoutMode = CheckoutMode.ON_AGENT
        cleanCheckout = true
        branchFilter = "+:<default>"
    }
    steps {
        script {
            name = "Deploy and verify IIS API"
            scriptContent = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/ci/deploy-api.ps1"
        }
    }
    triggers {
        finishBuildTrigger {
            buildType = "${ApiCi.id}"
            successfulOnly = true
            branchFilter = "+:<default>"
        }
    }
    dependencies {
        dependency(ApiCi) {
            snapshot {
                onDependencyFailure = FailureAction.FAIL_TO_START
                onDependencyCancel = FailureAction.CANCEL
            }
            artifacts {
                buildRule = sameChain()
                cleanDestination = true
                artifactRules = """
                    api/api.zip => incoming
                    api/api-manifest.json => incoming
                """.trimIndent()
            }
        }
    }
    requirements {
        contains("teamcity.agent.jvm.os.name", "Windows")
        equals("env.ZENX_ROLE", "deploy")
    }
})
