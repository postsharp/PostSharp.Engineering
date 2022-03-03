# Use Cases

## Table of contents

- [Use Cases](#use-cases)
  - [Table of contents](#table-of-contents)
  - [Dependency resolver](#dependency-resolver)
    - [Resolve dependencies from feed](#resolve-dependencies-from-feed)
    - [Resolve dependencies from the build server](#resolve-dependencies-from-the-build-server)
    - [Resolve local dependencies](#resolve-local-dependencies)
    - [Resolve transitive dependencies](#resolve-transitive-dependencies)
  - [Developer](#developer)
    - [Local build](#local-build)
    - [Fetch the latest build server dependencies](#fetch-the-latest-build-server-dependencies)
    - [Switch to local dependencies](#switch-to-local-dependencies)
  - [Build server user](#build-server-user)
    - [Manually triggered build](#manually-triggered-build)
    - [Manually triggered publishing](#manually-triggered-publishing)
  - [Build server](#build-server)
    - [Commit-triggered build of changes in the default branch](#commit-triggered-build-of-changes-in-the-default-branch)

## Dependency resolver

### Resolve dependencies from feed

- An exact version of a dependency fetched from available feeds can be defined.

### Resolve dependencies from the build server

- A branch for which the latest successful compatible build is found can be defined.
- The artifacts of such build are then fetched from the build server and are used as the source of the dependency.

### Resolve local dependencies

- Local build of a dependency can be used by another local build.

### Resolve transitive dependencies

- Transitive dependencies of any of the kinds defined above are resolved.

## Developer

### Local build

- The developer should always be able to build any repo locally, having the latest compatible dependencies available.
- No incompatibilities introduced in the dependencies should break the build.
- Incompatible changes will be ignored until fixed.
- The local build doesn't trigger any build on the build server.
  
### Fetch the latest build server dependencies

- When building a clean repo, the latest compatible build server dependencies are fetched.
- For subsequent builds, the same build server dependencies are used, without contacting the build server again.
- The developer should be able to fetch the latest compatible build server dependencies again.

### Switch to local dependencies

- The developer should be able to switch to the local build of a dependency using one simple command.

## Build server user

### Manually triggered build

- The build server user should be able to trigger any build manually.
- If there are changes in any direct or transitive dependency, a build of each of those is triggered and awaited.

### Manually triggered publishing

- The build server user should be able to trigger any publishing manually.
- Publishing first triggers and waits for a public build of the product being published and all dependencies.
- For each enclosing project, there will be a "Publish All" build configuration, which will trigger the cascade of publishing.

## Build server

### Commit-triggered build of changes in the default branch

- When a new change appears in the default branch of a repo, the build of the repo is triggered.
- If the build succeeds, a build of all consuming repos is triggered.
- The triggered build configuration(s) are set in the repo.
- Changes committed by TeamCity do not trigger the build.
