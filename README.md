# PostSharp Engineering

## Table of contents

- [PostSharp Engineering](#postsharp-engineering)
  - [Table of contents](#table-of-contents)
  - [Content](#content)
  - [Concepts](#concepts)
    - [Terminology](#terminology)
    - [Build and testing locally](#build-and-testing-locally)
    - [Versioning](#versioning)
      - [Objectives](#objectives)
      - [Configuring the version of the current product](#configuring-the-version-of-the-current-product)
    - [Configuring the version of dependent products or packages.](#configuring-the-version-of-dependent-products-or-packages)
    - [Using a local build of a referenced product](#using-a-local-build-of-a-referenced-product)
  - [Installation](#installation)
    - [Step 1. Edit global.json](#step-1-edit-globaljson)
    - [Step 2. Packaging.props](#step-2-packagingprops)
    - [Step 3. MainVersion.props](#step-3-mainversionprops)
    - [Step 4. Versions.props](#step-4-versionsprops)
    - [Step 5. Directory.Build.props](#step-5-directorybuildprops)
    - [Step 6. Directory.Build.targets](#step-6-directorybuildtargets)
    - [Step 7. Create the front-end build project](#step-7-create-the-front-end-build-project)
    - [Step 8. Create Build.ps1, the front-end build script](#step-8-create-buildps1-the-front-end-build-script)
    - [Step 9. Editing .gitignore](#step-9-editing-gitignore)
  - [Continuous integration](#continuous-integration)
    - [Artifacts](#artifacts)
    - [Commands](#commands)
    - [Required environment variables](#required-environment-variables)

## Content

This repository contains common development, build and publishing scripts. It produces two NuGet packages:
 
* _PostSharp.Engineering.BuildTools_ is meant to be added as a package reference from the facade C# build program.
  
* _PostSharp.Engineering.Sdk_ is meant to be used as an SDK project.

  * `AssemblyMetadata.targets`: Adds package versions to assembly metadata.
  * `BuildOptions.props`: Sets the compiler options like language version, nullability and other build options like output path.
  * `TeamCity.targets`: Enables build and tests reporting to TeamCity.
  * `SourceLink.props`: Enables SourceLink support.
  * `Coverage.props`:
    Enabled code coverage. This script should be imported in test projects only (not in projects being tested). This script
    adds a package to _coverlet_ so there is no need to have in in test projects (and these references should be removed).
  * `Assets.props`: defines an `AssetsDirectory` property. Icons (and in the future similar assets) are found under this directory.

Both packages must be used at the same time.


## Concepts

### Terminology

A _product_ is almost synonym for _repository_. There is a single product per repository, and the product name must be the same as the repository name. A product can contain several C# solutions.

### Build and testing locally

For details, do `Build.ps1` in PowerShell and read the help.

### Versioning

#### Objectives

A major goal of this SDK is to allow to build and test repositories that have references to other repositories _without_ having to publish the nuget package.
That is, it is possible and quite easy, with this SDK, to perform builds that reference local clones of repositories. All solutions or projects in the same product share have the same version.

#### Configuring the version of the current product

The product package version and package version suffix configuration is centralized in the `eng\MainVersion.props`
script via the `MainVersion` and `PackageVersionSuffix` properties, respectively. For RTM products, leave
the `PackageVersionSuffix` property value empty.

### Configuring the version of dependent products or packages.

Package dependencies versions configuration is centralized in the `eng\Versions.props` script. Each dependency version
is configured in a property named `<[DependencyName]Version>`, eg. `<SystemCollectionsImmutableVersion>`.

This property value is then available in all MSBuild project files in the repository and can be used in
the `PackageReference` items. For example:

```
<ItemGroup>
    <PackageReference Include="System.Collections.Immutable" Version="$(SystemCollectionsImmutableVersion)" />
</ItemGroup>
```

### Using a local build of a referenced product

Dependencies must be checked out under the same root directory (typically `c:\src`) under their canonic name.

Then, use `Build.ps1 dependencies local` to specify which dependencies should be run locally.

This will generate `eng/Dependencies.props`, which you should have imported in `eng/Versions.props`.


## Installation

The easiest way to get started is from this repo template: https://github.com/postsharp/PostSharp.Engineering.ProductTemplate.

### Step 1. Edit global.json

Add or update the reference to `PostSharp.Engineering.Sdk` in `global.json`.

```json
{
  "sdk": {
    "version": "5.0.206",
    "rollForward": "disable"
  },
  "msbuild-sdks": {
    "PostSharp.Engineering.Sdk": "1.0.0"
  }
}
```

### Step 2. Packaging.props


Create `eng\Packaging.props` file. The content should look like this:

```xml
<Project>

    <!-- Properties of NuGet packages-->
    <PropertyGroup>
        <Authors>PostSharp Technologies</Authors>
        <PackageProjectUrl>https://github.com/postsharp/Caravela</PackageProjectUrl>
        <PackageTags>PostSharp Caravela AOP</PackageTags>
        <PackageRequireLicenseAcceptance>true</PackageRequireLicenseAcceptance>
        <PackageIcon>PostSharpIcon.png</PackageIcon>
        <PackageLicenseFile>LICENSE.md</PackageLicenseFile>
    </PropertyGroup>

    <!-- Additional content of NuGet packages -->
    <ItemGroup>
        <None Include="$(MSBuildThisFileDirectory)..\PostSharpIcon.png" Visible="false" Pack="true" PackagePath="" />
        <None Include="$(MSBuildThisFileDirectory)..\LICENSE.md" Visible="false" Pack="true" PackagePath="" />
        <None Include="$(MSBuildThisFileDirectory)..\THIRD-PARTY-NOTICES.TXT" Visible="false" Pack="true" PackagePath="" />
    </ItemGroup>

</Project>
```

Make sure that all the files referenced in the previous step exist, or modify the file.

### Step 3. MainVersion.props

Create `eng\MainVersion.props` file. The content should look like:

```xml
<Project>
    <PropertyGroup>
        <MainVersion>0.3.6</MainVersion>
        <PackageVersionSuffix>-preview</PackageVersionSuffix>
    </PropertyGroup>
</Project>
```

### Step 4. Versions.props

Create `eng\Versions.props` file. The content should look like this (replace `My` by the name of the repo without dot):

```xml
<Project>

    <!-- Version of My. -->
    <Import Project="MainVersion.props" Condition="!Exists('MyVersion.props')" />
    
    <PropertyGroup>
        <MyVersion>$(MainVersion)$(PackageVersionSuffix)</MyVersion>
        <MyAssemblyVersion>$(MainVersion)</MyAssemblyVersion>
    </PropertyGroup>

    <!-- Versions of dependencies -->
    <PropertyGroup>
        <RoslynVersion>3.8.0</RoslynVersion>
        <CaravelaCompilerVersion>3.8.12-preview</CaravelaCompilerVersion>
        <MicrosoftCSharpVersion>4.7.0</MicrosoftCSharpVersion>
    </PropertyGroup>

    <!-- Overrides by local settings -->
    <Import Project="../artifacts/publish/private/MyVersion.props" Condition="Exists('../artifacts/publish/private/MyVersion.props')" />
    <Import Project="Dependencies.props" Condition="Exists('Dependencies.props')" />

    <!-- Other properties depending on the versions set above -->
    <PropertyGroup>
        <AssemblyVersion>$(MyAssemblyVersion)</AssemblyVersion>
        <Version>$(MyVersion)</Version>
    </PropertyGroup>
    

</Project>
```

### Step 5. Directory.Build.props

Add the following content to `Directory.Build.props`:

```xml
<Project>

  <PropertyGroup>
    <RepoDirectory>$(MSBuildThisFileDirectory)</RepoDirectory>
    <RepoKind>AzureRepos</RepoKind>
  </PropertyGroup>

  <Import Project="eng\Versions.props" />
  <Import Project="eng\Packaging.props" />

  <Import Sdk="PostSharp.Engineering.Sdk" Project="BuildOptions.props" />
  <Import Sdk="PostSharp.Engineering.Sdk" Project="CodeQuality.props" />
  <Import Sdk="PostSharp.Engineering.Sdk" Project="SourceLink.props" />

</Project>
```

### Step 6. Directory.Build.targets

Add the following content to `Directory.Build.targets`:

```xml
<Project>

  <Import Sdk="PostSharp.Engineering.Sdk"  Project="AssemblyMetadata.targets" />
  <Import Sdk="PostSharp.Engineering.Sdk"  Project="TeamCity.targets" />

</Project>
```

### Step 7. Create the front-end build project

Create a file `eng\src\Build.csproj` with the following content:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net6.0</TargetFramework>
        <AssemblyName>Build</AssemblyName>
        <GenerateDocumentationFile>false</GenerateDocumentationFile>
        <LangVersion>latest</LangVersion>
        <Nullable>enable</Nullable>
        <NoWarn>SA0001;CS8002</NoWarn>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="PostSharp.Engineering.BuildTools.csproj" Version="$(PostSharpEngineeringVersion)" />
    </ItemGroup>

</Project>
```

Create also a file `eng\src\Program.cs` with content that varies according to your repo. You can use all the power of C#
and PowerShell to customize the build. Note that in the `PublicArtifacts`, the strings `$(Configuration)`
and `$(PackageVersion)`, and only those strings, are replaced by their value.

```cs
using PostSharp.Engineering.BuildTools;
using PostSharp.Engineering.BuildTools.Commands.Build;
using Spectre.Console.Cli;
using System.Collections.Immutable;

namespace BuildCaravela
{
    internal class Program
    {
        private static int Main( string[] args )
        {
            var product = new Product
            {
                ProductName = "Caravela",
                Solutions = ImmutableArray.Create<Solution>(
                    new DotNetSolution( "Caravela.sln" )
                    {
                        SupportsTestCoverage = true
                    },
                    new DotNetSolution( "Tests\\Caravela.Framework.TestApp\\Caravela.Framework.TestApp.sln" )
                    {
                        IsTestOnly = true
                    } ),
                PublicArtifacts = ImmutableArray.Create(
                    "bin\\$(Configuration)\\Caravela.Framework.$(PackageVersion).nupkg",
                    "bin\\$(Configuration)\\Caravela.TestFramework.$(PackageVersion).nupkg",
                    "bin\\$(Configuration)\\Caravela.Framework.Redist.$(PackageVersion).nupkg",
                    "bin\\$(Configuration)\\Caravela.Framework.Sdk.$(PackageVersion).nupkg",
                    "bin\\$(Configuration)\\Caravela.Framework.Impl.$(PackageVersion).nupkg",
                    "bin\\$(Configuration)\\Caravela.Framework.DesignTime.Contracts.$(PackageVersion).nupkg" ),
                 Dependencies = ImmutableArray.Create(
                    new ProductDependency("Caravela.Compiler"), 
                    new ProductDependency("PostSharp.Engineering.BuildTools") )    
            };
            var commandApp = new CommandApp();
            commandApp.AddProductCommands( product );

            return commandApp.Run( args );
        }
    }
}
```

### Step 8. Create Build.ps1, the front-end build script

Create `Build.ps1` file in the repo root directory. The content should look like:

```powershell
if ( $env:VisualStudioVersion -eq $null ) {
    Import-Module "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\Tools\Microsoft.VisualStudio.DevShell.dll"
    Enter-VsDevShell -VsInstallPath "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\" -StartInPath $(Get-Location)
}

& dotnet run --project "$PSScriptRoot\eng\src\Build.csproj" -- $args
exit $LASTEXITCODE
```

### Step 9. Editing .gitignore

Exclude this:

```
artifacts
eng/tools
*.Import.props
```

## Continuous integration

We use TeamCity as our CI/CD pipeline, and we use Kotlin scripts stored in the Git repo. For an example, see the `.teamcity` directory
the current repo.

### Artifacts

All TeamCity artifacts are published under `artifacts/publish`. All build configurations should export and import these artifacts.

### Commands

All TeamCity build configurations use the front-end `Build.ps1`:

* Debug Build and Test: `Build.ps1 test --numbered  %build.number%`
* Release Build and Test:  `Build.ps1 test --public --sign`
* Publish to internal package sources:  `Build.ps1 publish`
* Publish to internal _and_ public package source:  `Build.ps1 publish --public`

### Required environment variables

- SIGNSERVER_SECRET
- INTERNAL_NUGET_PUSH_URL
- INTERNAL_NUGET_API_KEY
- NUGET_ORG_API_KEY
