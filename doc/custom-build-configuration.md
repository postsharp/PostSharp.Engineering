# Replacing a build configuration

The generated `Build [Debug|Release|Public]` configuration runs `Build.ps1 test --configuration <X>`: it builds from
source and depends on nothing else in the product. That is right for a product whose build is a build.

It is wrong for a product whose build consumes what an earlier build configuration of the same product produced.
PostSharp is the case in point. Its public build is the signed distribution, the packages that ship. It signs the
archives that an earlier configuration assembled, and compares the result against the unsigned baseline that the same
chain tested. A configuration that rebuilds from source does not produce the artifact it is named for.

Two members cover this: one build configuration can depend on another of the same product, and a product can replace
the standard build configuration with one of its own.

## Depending on another build configuration of the same product

`AdditionalCiBuildConfiguration.SnapshotDependencies` names the build configurations that this one waits for and
downloads artifacts from. Each entry carries its own artifact rules and its own clean-destination flag, because the
configurations of one pipeline carry different artifacts.

```csharp
new PowershellAdditionalCiBuildConfiguration( "BuildDistribution", "Build Distribution", "make.ps1", "DistributionFromArtifacts" )
{
    // Whose artifact layout the checkout is prepared for: whose nuget.restored.config is copied and whose version
    // file is imported. It is not the same question as what to depend on, and it has one answer per configuration.
    BuildSnapshotDependency = BuildConfiguration.Public,

    SnapshotDependencies =
    [
        new SnapshotDependency( "BuildArtifacts" )
        {
            ArtifactRules = ["+:artifacts/publish/private/**/*=>artifacts/publish/private", "+:PostSharp-*.7z!**=>"],

            // One of those rules unpacks into the checkout root, so the destination must not be cleaned: doing so
            // deletes the sources, and the build then fails on a missing file with nothing to say why.
            CleanDestination = false
        }
    ]
}
```

One rule per entry. They are joined for TeamCity by the generator, which is why a rule must not contain a line break:
the rules of one dependency are emitted into a single-line Kotlin string.

A dependency with `ArtifactRules` left null takes the whole private artifact directory as it is. An empty array
downloads nothing, which makes the dependency an ordering constraint only.

`SnapshotDependencies` replaces `BuildSnapshotDependencyId`, `DependencyArtifactRules` and
`CleanDependencyDestination`, which are obsolete: each was a single slot, so a configuration could name only one
upstream. `BuildSnapshotDependency` is not obsolete. Used alone it is the shortcut for the common case, a dependency
on one of the product's own build configurations with the default rules, and it remains the only way to state the
artifact layout.

## Replacing the standard build configuration

`BuildConfigurationInfo.CustomBuildConfiguration` names an `AdditionalCiBuildConfiguration` that becomes the
`Build [X]` build type.

```csharp
Configurations = Product.DefaultConfigurations.WithValue(
    BuildConfiguration.Public,
    c => c with
    {
        CustomBuildConfiguration = SignedDistribution,
        AdditionalArtifactRules = ["+:Build/intermediate/artifacts/*.7z=>artifacts/publish/private"]
    } ),
```

The replacement states what the build does: its steps, `Parameters`, `TimeoutInMinutes`, `BuildAgentRequirements`,
`Dockerfile` and its own `SnapshotDependencies`.

What identifies the build configuration to the rest of the product is not the replacement's to choose, and the
generator overrides it:

| Property | Why |
|---|---|
| Object name | TeamCity derives the build type identifier from it, and other products address this build by that identifier. |
| Display name | Governed by `TeamCityBuildName`, as for the standard configuration. |
| Default branch | The public build's default branch must not be the release branch, or the scheduled build fails to trigger on the development branch. |
| Artifact rules | The deployment reads the public and private artifact directories from this configuration by name. Extra rules belong in `AdditionalArtifactRules`. |
| Snapshot dependencies | The dependencies on other products belong to the product, not to the configuration that implements its build. The replacement's own dependencies are kept. |
| Source dependencies | Same reason. |
| Build triggers | Declared in `BuildConfigurationInfo.BuildTriggers`, which is where every build configuration declares them. |
| Commit status publisher | A build configuration reports its status to GitHub; without it, pull requests lose their check. |
| SSH agent | Requested only where the product would generate an upstream check, which a replacement does not run. |

Setting `ArtifactRules` or `BuildTriggers` on a configuration used as a replacement is an error rather than being
silently overridden, and so is listing it in `Product.AdditionalCiBuildConfigurations` as well, which would generate
the same Kotlin object twice.

The replacement runs no `PreKill`, `UpstreamCheck` or `PostKill` step. Those belong to the standard build step, and a
configuration that replaces it states its own steps. A product that would genuinely have generated an upstream check
is told to clear `RequiresUpstreamCheck`.

## Validation

`Build.ps1 generate-scripts` rejects the mistakes that TeamCity would otherwise find, and that it reports without
naming the declaration that caused them: an unknown identifier, a dependency on a configuration that is not exported,
a duplicate identifier, a self-dependency, a cycle, and an artifact layout that names a configuration the build does
not depend on. That last one is worth stating, because its failure is otherwise silent and late: the generated steps
would copy `nuget.restored.config` and import a version file from a directory the build never downloaded, and the
build would fail on a missing file with nothing to say why.
