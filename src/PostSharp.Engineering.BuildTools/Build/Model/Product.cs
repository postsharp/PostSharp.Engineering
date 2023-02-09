// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Extensions.FileSystemGlobbing;
using PostSharp.Engineering.BuildTools.Build.Publishers;
using PostSharp.Engineering.BuildTools.Build.Triggers;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Coverage;
using PostSharp.Engineering.BuildTools.Dependencies;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.NuGet;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class Product
    {
        public DependencyDefinition DependencyDefinition { get; }

        private readonly string? _versionsFile;

        private readonly string? _mainVersionFile;

        private readonly string? _bumpInfoFile;

        public Product( DependencyDefinition dependencyDefinition )
        {
            this.DependencyDefinition = dependencyDefinition;
#pragma warning disable CS0618 // Obsolete init accessor should cause warning only outside constructor.
            this.ProductName = dependencyDefinition.Name;
#pragma warning restore CS0618
            this.VcsProvider = dependencyDefinition.Repo.Provider;
            this.BuildExePath = Assembly.GetCallingAssembly().Location;
        }

        public string BuildExePath { get; }

        public string EngineeringDirectory => this.DependencyDefinition.EngineeringDirectory;

        public string VersionsFilePath
        {
            get => this._versionsFile ?? Path.Combine( this.EngineeringDirectory, "Versions.props" );
            init => this._versionsFile = value;
        }

        public string MainVersionFilePath
        {
            get => this._mainVersionFile ?? Path.Combine( this.EngineeringDirectory, "MainVersion.props" );
            init => this._mainVersionFile = value;
        }

        public string BumpInfoFilePath
        {
            get => this._bumpInfoFile ?? Path.Combine( this.EngineeringDirectory, "BumpInfo.txt" );
            init => this._bumpInfoFile = value;
        }

        /// <summary>
        /// Gets the dependency from which the main version should be copied.
        /// </summary>
        public DependencyDefinition? MainVersionDependency { get; init; }

        public string ProductName
        {
            get;
            [Obsolete( "Product name is set in constructor using DependencyDefinition." )]
            init;
        }

        public string ProductNameWithoutDot => this.ProductName.Replace( ".", "", StringComparison.OrdinalIgnoreCase );

        public ParametricString PrivateArtifactsDirectory { get; init; } = Path.Combine( "artifacts", "publish", "private" );

        public ParametricString PublicArtifactsDirectory { get; init; } = Path.Combine( "artifacts", "publish", "public" );

        public ParametricString TestResultsDirectory { get; init; } = Path.Combine( "artifacts", "testResults" );

        public ParametricString LogsDirectory { get; init; } = Path.Combine( "artifacts", "logs" );

        public ParametricString SourceDependenciesDirectory { get; init; } = Path.Combine( "source-dependencies" );

        public bool GenerateArcadeProperties { get; init; }

        public string[] AdditionalDirectoriesToClean { get; init; } = Array.Empty<string>();

        public Solution[] Solutions { get; init; } = Array.Empty<Solution>();

        public Pattern PrivateArtifacts { get; init; } = Pattern.Empty;

        public Pattern PublicArtifacts { get; init; } = Pattern.Empty;

        public VcsProvider? VcsProvider { get; }

        public bool KeepEditorConfig { get; init; }

        public string BuildAgentType { get; init; } = "caravela04";

        public ConfigurationSpecific<BuildConfigurationInfo> Configurations { get; init; } = DefaultConfigurations;

        public static ImmutableArray<Publisher> DefaultPublicPublishers { get; }
            = ImmutableArray.Create(
                new Publisher[]
                {
                    new NugetPublisher( Pattern.Create( "*.nupkg" ), "https://api.nuget.org/v3/index.json", "%NUGET_ORG_API_KEY%" ),
                    new VsixPublisher( Pattern.Create( "*.vsix" ) )
                } );

        public static ConfigurationSpecific<BuildConfigurationInfo> DefaultConfigurations { get; }
            = new(
                debug: new BuildConfigurationInfo( MSBuildName: "Debug", BuildTriggers: new IBuildTrigger[] { new SourceBuildTrigger() } ),
                release: new BuildConfigurationInfo( MSBuildName: "Release", RequiresSigning: true, ExportsToTeamCityBuild: false ),
                @public: new BuildConfigurationInfo(
                    MSBuildName: "Release",
                    RequiresSigning: true,
                    PublicPublishers: DefaultPublicPublishers.ToArray(),
                    ExportsToTeamCityDeploy: true ) );

        public ImmutableArray<string> DefaultArtifactRules { get; } =
            ImmutableArray.Create(
                $@"+:artifacts/logs/**/*=>logs",
                $@"+:%system.teamcity.build.tempDir%/Metalama/AssemblyLocator/**/*=>logs",
                $@"+:%system.teamcity.build.tempDir%/Metalama/CompileTime/**/.completed=>logs",
                $@"+:%system.teamcity.build.tempDir%/Metalama/CompileTimeTroubleshooting/**/*=>logs",
                $@"+:%system.teamcity.build.tempDir%/Metalama/CrashReports/**/*=>logs",
                $@"+:%system.teamcity.build.tempDir%/Metalama/Extract/**/.completed=>logs",
                $@"+:%system.teamcity.build.tempDir%/Metalama/ExtractExceptions/**/*=>logs",
                $@"+:%system.teamcity.build.tempDir%/Metalama/Logs/**/*=>logs" );

        /// <summary>
        /// List of properties that must be exported into the *.version.props. These properties must be defined in MainVersion.props.
        /// </summary>
        public string[] ExportedProperties { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the set of artifact dependencies of this product. Some commands expect the dependency to exist in <see cref="PostSharp.Engineering.BuildTools.Dependencies.Model.Dependencies.All"/>.
        /// </summary>
        public DependencyDefinition[] Dependencies { get; init; } = Array.Empty<DependencyDefinition>();

        /// <summary>
        /// Gets the set of source code dependencies of this product. 
        /// </summary>
        public DependencyDefinition[] SourceDependencies { get; init; } = Array.Empty<DependencyDefinition>();

        public IBumpStrategy BumpStrategy { get; init; } = new DefaultBumpStrategy();

        public DependencyDefinition? GetDependency( string name )
        {
            return this.Dependencies.SingleOrDefault( d => d.Name == name )
                   ?? BuildTools.Dependencies.Model.Dependencies.All.SingleOrDefault( d => d.Name == name )
                   ?? TestDependencies.All.SingleOrDefault( d => d.Name == name );
        }

        public Dictionary<string, string> SupportedProperties { get; init; } = new();

        public bool RequiresEngineeringSdk { get; init; } = true;

        public ImmutableArray<DotNetTool> DotNetTools { get; init; } = DotNetTool.DefaultTools;

        public bool Build( BuildContext context, BuildSettings settings )
        {
            var configuration = settings.BuildConfiguration;
            var buildConfigurationInfo = this.Configurations[configuration];

            // Build dependencies.
            if ( !settings.NoDependencies && !this.Prepare( context, settings ) )
            {
                return false;
            }

            // Delete the root import file in the repo because the presence of this file means a successful build.
            this.DeleteImportFile( context );

            // We have to read the version from the file we have generated - using MSBuild, because it contains properties.
            var versionInfo = this.ReadGeneratedVersionFile( context.GetManifestFilePath( configuration ) );

            var privateArtifactsDir = Path.Combine(
                context.RepoDirectory,
                this.PrivateArtifactsDirectory.ToString( versionInfo ) );

            // Build.
            if ( !this.BuildCore( context, settings ) )
            {
                return false;
            }

            // Allow for some customization before we create the zip file and copy to the public directory.
            var eventArgs = new BuildCompletedEventArgs( context, settings, privateArtifactsDir );
            this.BuildCompleted?.Invoke( eventArgs );

            if ( eventArgs.IsFailed )
            {
                return false;
            }

            // Check that the build produced the expected artifacts.
            var allFilesPattern = this.PublicArtifacts.Appends( this.PrivateArtifacts );

            if ( !allFilesPattern.Verify( context, privateArtifactsDir, versionInfo ) )
            {
                return false;
            }

            // Zipping internal artifacts.
            void CreateZip( string directory )
            {
                if ( settings.CreateZip )
                {
                    var zipFile = Path.Combine( directory, $"{this.ProductName}-{versionInfo.PackageVersion}.zip" );

                    context.Console.WriteMessage( $"Creating '{zipFile}'." );
                    var tempFile = Path.Combine( Path.GetTempPath(), Guid.NewGuid() + ".zip" );

                    ZipFile.CreateFromDirectory(
                        directory,
                        tempFile,
                        CompressionLevel.Optimal,
                        false );

                    File.Move( tempFile, zipFile );
                }
            }

            CreateZip( privateArtifactsDir );

            // Copy public artifacts to the publish directory.
            var publicArtifactsDirectory = Path.Combine(
                context.RepoDirectory,
                this.PublicArtifactsDirectory.ToString( versionInfo ) );

            if ( !Directory.Exists( publicArtifactsDirectory ) )
            {
                Directory.CreateDirectory( publicArtifactsDirectory );
            }

            void CreateEmptyPublicDirectory()
            {
                // We have to create an empty file, otherwise TeamCity will complain that
                // artifacts are missing.
                var emptyFile = Path.Combine( publicArtifactsDirectory, ".empty" );

                File.WriteAllText( emptyFile, "This file is intentionally empty." );
            }

            if ( this.PublicArtifacts.IsEmpty )
            {
                context.Console.WriteMessage( "Do not prepare public artifacts because there is none." );
                CreateEmptyPublicDirectory();
            }
            else if ( settings.BuildConfiguration != BuildConfiguration.Public )
            {
                context.Console.WriteMessage( "Do not prepare public artifacts because this is not a public build" );
                CreateEmptyPublicDirectory();
            }
            else
            {
                // Copy artifacts.
                context.Console.WriteHeading( "Copying public artifacts" );
                var files = new List<FilePatternMatch>();

                this.PublicArtifacts.TryGetFiles( privateArtifactsDir, versionInfo, files );

                foreach ( var file in files )
                {
                    var targetFile = Path.Combine( publicArtifactsDirectory, Path.GetFileName( file.Path ) );

                    context.Console.WriteMessage( file.Path );
                    File.Copy( Path.Combine( privateArtifactsDir, file.Path ), targetFile, true );
                }

                var signSuccess = true;

                if ( buildConfigurationInfo.RequiresSigning && !settings.NoSign )
                {
                    context.Console.WriteHeading( "Signing artifacts" );

                    var signToolSecret = Environment.GetEnvironmentVariable( "SIGNSERVER_SECRET" );

                    if ( signToolSecret == null )
                    {
                        context.Console.WriteError( "The SIGNSERVER_SECRET environment variable is not defined." );

                        return false;
                    }

                    void Sign( string filter )
                    {
                        if ( Directory.EnumerateFiles( publicArtifactsDirectory, filter ).Any() )
                        {
                            // We don't pass the secret so it does not get printed. We pass an environment variable reference instead.
                            // The ToolInvocationHelper will expand it.

                            signSuccess = signSuccess && DotNetTool.SignClient.Invoke(
                                context,
                                $"Sign --baseDirectory \"{publicArtifactsDirectory}\" --input {filter}" );
                        }
                    }

                    Sign( "*.nupkg" );
                    Sign( "*.vsix" );

                    if ( !signSuccess )
                    {
                        return false;
                    }

                    // Zipping public artifacts.
                    CreateZip( publicArtifactsDirectory );

                    context.Console.WriteSuccess( "Signing artifacts was successful." );
                }
            }

            // Create the consolidate directory.
            if ( settings.CreateConsolidatedDirectory )
            {
                context.Console.WriteHeading( "Creating the consolidated directory" );

                var consolidatedDirectory = Path.Combine(
                    context.RepoDirectory,
                    "artifacts",
                    "consolidated",
                    configuration.ToString().ToLowerInvariant() );

                if ( Directory.Exists( consolidatedDirectory ) )
                {
                    Directory.Delete( consolidatedDirectory, true );
                }

                Directory.CreateDirectory( consolidatedDirectory );

                context.Console.WriteMessage( $"Creating '{consolidatedDirectory}'." );

                // Copy dependencies.
                if ( !DependenciesOverrideFile.TryLoad( context, configuration, out var dependenciesOverrideFile ) )
                {
                    return false;
                }

                foreach ( var dependency in dependenciesOverrideFile.Dependencies )
                {
                    if ( dependency.Value.VersionFile != null )
                    {
                        var versionDocument = XDocument.Load( dependency.Value.VersionFile );
                        var import = versionDocument.Root!.Element( "Import" )?.Attribute( "Project" )?.Value;

                        string importDirectory;

                        if ( import == null )
                        {
                            importDirectory = Path.GetDirectoryName( dependency.Value.VersionFile )!;
                        }
                        else
                        {
                            importDirectory = Path.GetDirectoryName( Path.Combine( Path.GetDirectoryName( dependency.Value.VersionFile )!, import ) )!;
                        }

                        CopyPackages( importDirectory );
                    }
                }

                // Copy current repo.
                CopyPackages( privateArtifactsDir );

                void CopyPackages( string directory )
                {
                    foreach ( var file in Directory.GetFiles( directory, "*.nupkg" ) )
                    {
                        File.Copy( file, Path.Combine( consolidatedDirectory, Path.GetFileName( file ) ), true );
                    }
                }
            }

            // Writing the import file at the end of the build so it gets only written if the build was successful.
            this.WriteImportFile( context, configuration );

            context.Console.WriteSuccess( $"Building the whole {this.ProductName} product was successful. Package version: {versionInfo.PackageVersion}." );

            return true;
        }

        private void DeleteImportFile( BuildContext context )
        {
            var importFilePath = Path.Combine( context.RepoDirectory, this.ProductName + ".Import.props" );

            if ( File.Exists( importFilePath ) )
            {
                File.Delete( importFilePath );
            }
        }

        private void WriteImportFile( BuildContext context, BuildConfiguration configuration )
        {
            // Write a link to this file in the root file of the repo. This file is the interface of the repo, which can be imported by other repos.
            var manifestFilePath = context.GetManifestFilePath( configuration );
            var importFilePath = Path.Combine( context.RepoDirectory, this.ProductName + ".Import.props" );

            // We're generating a relative path so that the path can be resolved even when the filesystem is mounted
            // to a different location than the current one (used e.g. when using Hyper-V).
            var relativePath = Path.GetRelativePath( Path.GetDirectoryName( importFilePath )!, manifestFilePath );

            var importFileContent = $@"
<!-- File generated by PostSharp.Engineering  {VersionHelper.EngineeringVersion}. -->
<Project>
    <!-- This file must not be added to source control and must not be uploaded as a build artifact.
         It must be imported by other repos as a dependency. 
         Dependent projects should not directly reference the artifacts path, which is considered an implementation detail. -->
    <Import Project=""{relativePath}""/>
</Project>
";

            File.WriteAllText( importFilePath, importFileContent );
        }

        private BuildInfo ReadGeneratedVersionFile( string path )
        {
            var versionFilePath = path;
            var versionFile = Project.FromFile( versionFilePath, new ProjectOptions() );

            string packageVersion;

            if ( this.GenerateArcadeProperties )
            {
                packageVersion = versionFile.Properties
                    .Single( p => p.Name == this.ProductNameWithoutDot + "VersionPrefix" )
                    .EvaluatedValue;

                var suffix = versionFile
                    .Properties
                    .Single( p => p.Name == this.ProductNameWithoutDot + "VersionSuffix" )
                    .EvaluatedValue;

                if ( !string.IsNullOrWhiteSpace( suffix ) )
                {
                    packageVersion = packageVersion + "-" + suffix;
                }
            }
            else
            {
                packageVersion = versionFile
                    .Properties
                    .Single( p => p.Name == this.ProductNameWithoutDot + "Version" )
                    .EvaluatedValue;
            }

            if ( string.IsNullOrEmpty( packageVersion ) )
            {
                throw new InvalidOperationException( "PackageVersion should not be null." );
            }

            var configuration = versionFile
                .Properties
                .Single( p => p.Name == this.ProductNameWithoutDot + "BuildConfiguration" )
                .EvaluatedValue;

            if ( string.IsNullOrEmpty( configuration ) )
            {
                throw new InvalidOperationException( "BuildConfiguration should not be null." );
            }

            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

            return new BuildInfo( packageVersion, Enum.Parse<BuildConfiguration>( configuration ), this );
        }

        public record MainVersionFileInfo(
            string MainVersion,
            string? OverriddenPatchVersion,
            string PackageVersionSuffix,
            int? OurPatchVersion,
            ImmutableDictionary<string, string?> ExportedProperties );

        /// <summary>
        /// Reads MainVersion.props but does not interpret anything.
        /// </summary>
        public MainVersionFileInfo ReadMainVersionFile( string path )
        {
            var versionFilePath = path;
            var versionFile = Project.FromFile( versionFilePath, new ProjectOptions() );

            var mainVersion = versionFile
                .Properties
                .SingleOrDefault( p => p.Name == "MainVersion" )
                ?.EvaluatedValue;

            var overriddenPatchVersion = versionFile
                .Properties
                .SingleOrDefault( p => p.Name == "OverriddenPatchVersion" )
                ?.EvaluatedValue;

            var ourPatchVersion = versionFile
                .Properties
                .SingleOrDefault( p => p.Name == "OurPatchVersion" )
                ?.EvaluatedValue;

            if ( string.IsNullOrEmpty( mainVersion ) )
            {
                throw new InvalidOperationException( $"MainVersion should not be null in '{path}'." );
            }

            var suffix = versionFile
                             .Properties
                             .SingleOrDefault( p => p.Name == "PackageVersionSuffix" )
                             ?.EvaluatedValue
                         ?? "";

            // Empty suffixes are allowed and mean RTM.

            // Read exported properties.
            var exportedProperties = ImmutableDictionary.CreateBuilder<string, string?>();

            foreach ( var exportedPropertyName in this.ExportedProperties )
            {
                var exportedPropertyValue = versionFile
                    .Properties
                    .SingleOrDefault( p => string.Equals( p.Name, exportedPropertyName, StringComparison.OrdinalIgnoreCase ) )
                    ?.EvaluatedValue;

                exportedProperties[exportedPropertyName] = exportedPropertyValue;
            }

            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

            return new MainVersionFileInfo(
                mainVersion,
                overriddenPatchVersion,
                suffix,
                ourPatchVersion != null ? int.Parse( ourPatchVersion, CultureInfo.InvariantCulture ) : null,
                exportedProperties.ToImmutable() );
        }

        /// <summary>
        /// An event raised when the build is completed.
        /// </summary>
        public event Action<BuildCompletedEventArgs>? BuildCompleted;

        /// <summary>
        /// An event raised when the Prepare phase is complete.
        /// </summary>
        public event Action<PrepareCompletedEventArgs>? PrepareCompleted;

        protected virtual bool BuildCore( BuildContext context, BuildSettings settings )
        {
            IEnumerable<Solution> solutionsToBuild;

            if ( settings.SolutionId != null )
            {
                var solution = this.Solutions[settings.SolutionId.Value - 1];
                solutionsToBuild = new[] { solution };
            }
            else
            {
                solutionsToBuild = this.Solutions;
            }

            foreach ( var solution in solutionsToBuild )
            {
                if ( settings.IncludeTests || !solution.IsTestOnly )
                {
                    context.Console.WriteHeading( $"Building {solution.Name} ({settings.BuildConfiguration} configuration)" );

                    if ( !settings.NoDependencies )
                    {
                        if ( !solution.Restore( context, settings ) )
                        {
                            return false;
                        }
                    }

                    var buildMethod = solution.GetBuildMethod();

                    if ( !TryExecuteBuildMethod( context, settings, solution, buildMethod ) )
                    {
                        return false;
                    }

                    context.Console.WriteSuccess( $"Building {solution.Name} was successful." );
                }
            }

            return true;
        }

        private static bool TryExecuteBuildMethod( BuildContext context, BuildSettings settings, Solution solution, BuildMethod buildMethod )
        {
            switch ( buildMethod )
            {
                case BuildMethod.None:
                    return true;

                case BuildMethod.Build:
                    return solution.Build( context, settings );

                case BuildMethod.Pack:
                    if ( solution.PackRequiresExplicitBuild && !settings.NoDependencies )
                    {
                        if ( !solution.Build( context, settings ) )
                        {
                            return false;
                        }
                    }

                    return solution.Pack( context, settings );

                case BuildMethod.Test:
                    return solution.Test( context, settings );

                default:
                    throw new NotImplementedException( $"Build method '{buildMethod}' is not implemented." );
            }
        }

        public bool Test( BuildContext context, BuildSettings settings )
        {
            if ( !settings.NoDependencies && !this.Build( context, settings ) )
            {
                return false;
            }

            ImmutableDictionary<string, string> properties;
            var testResultsDir = Path.Combine( context.RepoDirectory, "TestResults" );

            if ( settings.AnalyzeCoverage )
            {
                // Removing the TestResults directory so that we reset the code coverage information.
                if ( Directory.Exists( testResultsDir ) )
                {
                    Directory.Delete( testResultsDir, true );
                }

                properties = settings.AnalyzeCoverage
                    ? ImmutableDictionary.Create<string, string>()
                        .Add( "CollectCoverage", "True" )
                        .Add( "CoverletOutput", testResultsDir + "\\" )
                    : ImmutableDictionary<string, string>.Empty;
            }
            else
            {
                properties = ImmutableDictionary<string, string>.Empty;
            }
            
            Solution[] solutionsToTest;
            
            if ( settings.SolutionId != null )
            {
                var solution = this.Solutions[settings.SolutionId.Value - 1];
                solutionsToTest = new[] { solution };
            }
            else
            {
                solutionsToTest = this.Solutions;
            }
            
            foreach ( var solution in solutionsToTest )
            {
                var solutionSettings = settings;

                if ( settings.AnalyzeCoverage && solution.SupportsTestCoverage )
                {
                    solutionSettings =
                        settings.WithAdditionalProperties( properties ).WithoutConcurrency();
                }

                context.Console.WriteHeading( $"Testing {solution.Name}." );

                if ( !TryExecuteBuildMethod( context, solutionSettings, solution, solution.TestMethod ?? BuildMethod.Test ) )
                {
                    return false;
                }

                context.Console.WriteSuccess( $"Testing {solution.Name} was successful" );
            }

            if ( settings.AnalyzeCoverage )
            {
                if ( !AnalyzeCoverageCommand.Execute(
                        context.Console,
                        new AnalyzeCoverageCommandSettings { Path = Path.Combine( testResultsDir, "coverage.net5.0.json" ) } ) )
                {
                    return false;
                }
            }

            var testResultsDirectory = Path.Combine( context.RepoDirectory, this.TestResultsDirectory.ToString() );

            if ( !Directory.Exists( testResultsDirectory ) )
            {
                Directory.CreateDirectory( testResultsDirectory );
            }

            if ( !Directory.GetFiles( testResultsDirectory ).Any() )
            {
                // We have to create an empty file, otherwise TeamCity will complain that
                // artifacts are missing.
                var emptyFile = Path.Combine( testResultsDirectory, ".empty" );

                File.WriteAllText( emptyFile, "This file is intentionally empty." );
            }

            context.Console.WriteSuccess( $"Testing {this.ProductName} was successful" );

            return true;
        }

        public string GetConfigurationNeutralVersionsFilePath( BuildContext context )
        {
            return Path.Combine( context.RepoDirectory, this.EngineeringDirectory, "Versions.g.props" );
        }

        public BuildConfiguration? ReadDefaultConfiguration( BuildContext context )
        {
            var path = this.GetConfigurationNeutralVersionsFilePath( context );

            if ( !File.Exists( path ) )
            {
                return null;
            }

            var versionFile = Project.FromFile( path, new ProjectOptions() );

            var configuration = versionFile.Properties.SingleOrDefault( p => p.Name == "EngineeringConfiguration" )
                ?.UnevaluatedValue;

            if ( configuration == null )
            {
                return null;
            }

            // Note that the version suffix is not copied from the dependency, only the main version. 

            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

            return Enum.Parse<BuildConfiguration>( configuration );
        }

        public bool Prepare( BuildContext context, BuildSettings settings )
        {
            if ( !settings.NoDependencies )
            {
                this.Clean( context, settings );
            }

            if ( settings.BuildConfiguration == BuildConfiguration.Public && !TeamCityHelper.IsTeamCityBuild( settings ) && !settings.Force )
            {
                context.Console.WriteError(
                    "Cannot prepare a public configuration on a local machine without --force because it may corrupt the package cache." );

                return false;
            }

            // Prepare the versions file.
            if ( !this.PrepareVersionsFile( context, settings, out var packageVersion ) )
            {
                return false;
            }

            // Generating the TeamCity file.
            if ( !this.GenerateTeamcityConfiguration( context, packageVersion ) )
            {
                return false;
            }

            // Restore source dependencies.
            if ( this.SourceDependencies.Length > 0 )
            {
                if ( !this.RestoreSourceDependencies( context ) )
                {
                    return false;
                }
            }

            // Execute the event.
            if ( this.PrepareCompleted != null )
            {
                var eventArgs = new PrepareCompletedEventArgs( context, settings );
                this.PrepareCompleted( eventArgs );

                if ( eventArgs.IsFailed )
                {
                    return false;
                }
            }

            context.Console.WriteSuccess(
                $"Preparing the build was successful. {this.ProductNameWithoutDot}Version={this.ReadGeneratedVersionFile( context.GetManifestFilePath( settings.BuildConfiguration ) ).PackageVersion}" );

            return true;
        }

        private bool RestoreSourceDependencies( BuildContext context )
        {
            var sourceDependenciesDirectory = Path.Combine( context.RepoDirectory, "source-dependencies" );

            if ( !Directory.Exists( sourceDependenciesDirectory ) )
            {
                Directory.CreateDirectory( sourceDependenciesDirectory );
            }

            foreach ( var dependency in this.SourceDependencies )
            {
                context.Console.WriteMessage( $"Restoring '{dependency.Name}' source dependency." );

                var localDirectory = Path.Combine( context.RepoDirectory, "..", dependency.Name );

                var targetDirectory = Path.Combine( sourceDependenciesDirectory, dependency.Name );

                if ( Directory.Exists( localDirectory ) )
                {
                    if ( !Directory.Exists( targetDirectory ) )
                    {
                        context.Console.WriteMessage( $"Creating symbolic link to '{localDirectory}' in '{targetDirectory}'." );
                        Directory.CreateSymbolicLink( targetDirectory, localDirectory );

                        if ( !Directory.Exists( targetDirectory ) )
                        {
                            context.Console.WriteError( $"Symbolic link was not created for '{targetDirectory}'." );

                            return false;
                        }
                    }
                }
                else
                {
                    if ( !Directory.Exists( targetDirectory ) )
                    {
                        // If the target directory doesn't exist, we clone it to the source-dependencies directory with depth of 1 to mitigate the impact of cloning the whole history.
                        if ( !ToolInvocationHelper.InvokeTool(
                                context.Console,
                                "git",
                                $"clone {dependency.Repo.RepoUrl} --branch {dependency.DefaultBranch} --depth 1",
                                sourceDependenciesDirectory ) )
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // If the target directory exists, we only pull the latest changes.
                        if ( !ToolInvocationHelper.InvokeTool(
                                context.Console,
                                "git",
                                $"pull",
                                targetDirectory ) )
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public bool PrepareVersionsFile( BuildContext context, BuildSettings settings, out string preparedPackageVersion )
        {
            var configuration = settings.BuildConfiguration;
            preparedPackageVersion = string.Empty;

            context.Console.WriteHeading( "Preparing the version file" );

            var privateArtifactsRelativeDir =
                this.PrivateArtifactsDirectory.ToString( new BuildInfo( null!, configuration, this ) );

            var artifactsDir = Path.Combine( context.RepoDirectory, privateArtifactsRelativeDir );

            if ( !Directory.Exists( artifactsDir ) )
            {
                Directory.CreateDirectory( artifactsDir );
            }

            var propsFileName = $"{this.ProductName}.version.props";
            var propsFilePath = Path.Combine( artifactsDir, propsFileName );

            // Load Versions.g.props.
            if ( !DependenciesOverrideFile.TryLoad( context, configuration, out var dependenciesOverrideFile ) )
            {
                return false;
            }

            // If we have any non-feed dependency that does not have a resolved VersionFile, it means that we have not fetched yet. 
            if ( dependenciesOverrideFile.Dependencies.Any( d => d.Value.SourceKind != DependencySourceKind.Feed && d.Value.VersionFile == null ) )
            {
                BaseFetchDependencyCommand.UpdateOrFetchDependencies( context, configuration, dependenciesOverrideFile, false );

                dependenciesOverrideFile.LocalBuildFile = propsFilePath;
            }

            // We always save the Versions.g.props because it may not exist and it may have been changed by the previous step.
            dependenciesOverrideFile.LocalBuildFile = propsFilePath;

            if ( !dependenciesOverrideFile.TrySave( context ) )
            {
                return false;
            }

            // Read the main version number.
            var mainVersionFileInfo = this.ReadMainVersionFile(
                Path.Combine(
                    context.RepoDirectory,
                    this.EngineeringDirectory,
                    "MainVersion.props" ) );

            if ( !this.TryComputeVersion( context, settings, configuration, mainVersionFileInfo, dependenciesOverrideFile, out var version ) )
            {
                return false;
            }

            // Generate Versions.g.props.
            var props = this.GenerateManifestFile( version, configuration, mainVersionFileInfo, dependenciesOverrideFile );
            context.Console.WriteMessage( $"Writing '{propsFilePath}'." );
            File.WriteAllText( propsFilePath, props );

            // Generating the configuration-neutral Versions.g.props for the prepared configuration.
            var configurationNeutralVersionsFilePath = this.GetConfigurationNeutralVersionsFilePath( context );

            context.Console.WriteMessage( $"Writing '{configurationNeutralVersionsFilePath}'." );

            File.WriteAllText(
                configurationNeutralVersionsFilePath,
                $@"
<!-- File generated by PostSharp.Engineering  {VersionHelper.EngineeringVersion}. -->
<Project>
    <PropertyGroup>
        <EngineeringConfiguration>{settings.BuildConfiguration}</EngineeringConfiguration>
    </PropertyGroup>
    <Import Project=""Versions.{settings.BuildConfiguration}.g.props"" Condition=""'$(DoNotLoadGeneratedVersionFiles)'!='True' AND Exists('Versions.{settings.BuildConfiguration}.g.props')""/>
</Project>
" );

            return true;
        }

        private bool TryComputeVersion(
            BuildContext context,
            BuildSettings settings,
            BuildConfiguration configuration,
            MainVersionFileInfo mainVersionFileInfo,
            DependenciesOverrideFile dependenciesOverrideFile,
            [NotNullWhen( true )] out VersionComponents? version )
        {
            var configurationLowerCase = configuration.ToString().ToLowerInvariant();

            version = null;
            string? mainVersion = null;

            if ( this.MainVersionDependency != null )
            {
                var mainVersionDependencyName = this.MainVersionDependency.Name;

                // The main version is defined in a dependency. Load the import file.

                if ( !dependenciesOverrideFile.Dependencies.TryGetValue( mainVersionDependencyName, out var dependencySource ) )
                {
                    context.Console.WriteError( $"Cannot find a dependency named '{mainVersionDependencyName}'." );

                    return false;
                }
                else if ( dependencySource.VersionFile == null )
                {
                    context.Console.WriteError( $"The dependency '{mainVersionDependencyName}' is not resolved." );

                    return false;
                }

                var versionFile = Project.FromFile( dependencySource.VersionFile, new ProjectOptions() );

                var propertyName = this.MainVersionDependency!.NameWithoutDot + "MainVersion";

                mainVersion = versionFile.Properties.SingleOrDefault( p => p.Name == propertyName )
                    ?.UnevaluatedValue;

                if ( string.IsNullOrEmpty( mainVersion ) )
                {
                    context.Console.WriteError( $"The file '{dependencySource.VersionFile}' does not contain the {propertyName}." );

                    return false;
                }

                // Note that the version suffix is not copied from the dependency, only the main version. 

                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            }

            if ( !string.IsNullOrEmpty( mainVersionFileInfo.OverriddenPatchVersion )
                 && !mainVersionFileInfo.OverriddenPatchVersion.StartsWith( mainVersion ?? mainVersionFileInfo.MainVersion + ".", StringComparison.Ordinal ) )
            {
                context.Console.WriteError(
                    $"The OverriddenPatchVersion property in MainVersion.props ({mainVersionFileInfo.OverriddenPatchVersion}) does not match the MainVersion property value ({mainVersion ?? mainVersionFileInfo.MainVersion})." );

                return false;
            }

            var versionPrefix = mainVersion ?? mainVersionFileInfo.MainVersion;
            string versionSuffix;
            int patchNumber;

            var versionSpec = settings.GetVersionSpec( configuration );
            var versionSpecKind = versionSpec.Kind;

            if ( configuration == BuildConfiguration.Public )
            {
                versionSpecKind = VersionKind.Public;
            }

            switch ( versionSpecKind )
            {
                case VersionKind.Local:
                    {
                        // Local build with timestamp-based version and randomized package number. For the assembly version we use a local incremental file stored in the user profile.
                        var localVersionDirectory =
                            Environment.ExpandEnvironmentVariables( "%APPDATA%\\Metalama.Engineering" );

                        var localVersionFile = $"{localVersionDirectory}\\{this.ProductName}.version";
                        int localVersion;

                        if ( File.Exists( localVersionFile ) )
                        {
                            localVersion = int.Parse(
                                File.ReadAllText( localVersionFile ),
                                CultureInfo.InvariantCulture ) + 1;
                        }
                        else
                        {
                            localVersion = 1;
                        }

                        if ( localVersion < 1000 )
                        {
                            localVersion = 1000;
                        }

                        if ( !Directory.Exists( localVersionDirectory ) )
                        {
                            Directory.CreateDirectory( localVersionDirectory );
                        }

                        File.WriteAllText( localVersionFile, localVersion.ToString( CultureInfo.InvariantCulture ) );

                        versionSuffix = $"local-{Environment.UserName}-{configurationLowerCase}";

                        patchNumber = localVersion;

                        break;
                    }

                case VersionKind.Numbered:
                    {
                        // Build server build with a build number given by the build server
                        patchNumber = versionSpec.Number;
                        versionSuffix = $"dev-{configurationLowerCase}";

                        break;
                    }

                case VersionKind.Public:
                    // Public build
                    versionSuffix = mainVersionFileInfo.PackageVersionSuffix.TrimStart( '-' );
                    patchNumber = 0;

                    if ( !string.IsNullOrWhiteSpace( mainVersionFileInfo.OverriddenPatchVersion ) )
                    {
                        var parsedOverriddenPatchedVersion = Version.Parse( mainVersionFileInfo.OverriddenPatchVersion );
                        patchNumber = parsedOverriddenPatchedVersion.Revision;
                    }

                    break;

                default:
                    throw new InvalidOperationException();
            }

            version = new VersionComponents( mainVersion ?? mainVersionFileInfo.MainVersion, versionPrefix, patchNumber, versionSuffix );

            return true;
        }

        private string GenerateManifestFile(
            VersionComponents version,
            BuildConfiguration configuration,
            MainVersionFileInfo mainVersionFileInfo,
            DependenciesOverrideFile dependenciesOverrideFile )
        {
            string packageVersion;

            var props = $@"
<!-- File generated by PostSharp.Engineering  {VersionHelper.EngineeringVersion}. -->
<Project>
    <PropertyGroup>
        <{this.ProductNameWithoutDot}MainVersion>{version.MainVersion}</{this.ProductNameWithoutDot}MainVersion>";

            var packageVersionWithoutSuffix = version.PatchNumber == 0 ? version.VersionPrefix : version.VersionPrefix + "." + version.PatchNumber;
            var assemblyVersion = version.VersionPrefix + "." + version.PatchNumber;

            if ( this.GenerateArcadeProperties )
            {
                // Metalama.Compiler, because of Arcade, requires the version number to be decomposed in a prefix, patch number, and suffix.
                // In Arcade, the package naming scheme is different because the patch number is not a part of the package name.

                var arcadeSuffix = "";

                if ( !string.IsNullOrEmpty( version.VersionSuffix ) )
                {
                    arcadeSuffix += version.VersionSuffix;
                }

                if ( version.PatchNumber > 0 )
                {
                    if ( arcadeSuffix.Length > 0 )
                    {
                        arcadeSuffix += "-";
                    }
                    else
                    {
                        // It should not happen that we have a patch number without a suffix.
                        arcadeSuffix += "-patch-" + configuration;
                    }

                    arcadeSuffix += version.PatchNumber;
                }

                var packageSuffixWithDash = string.IsNullOrEmpty( arcadeSuffix ) ? "" : "-" + arcadeSuffix;
                packageVersion = version.VersionPrefix + packageSuffixWithDash;

                props += $@"
        
        <{this.ProductNameWithoutDot}VersionPrefix>{version.VersionPrefix}</{this.ProductNameWithoutDot}VersionPrefix>
        <{this.ProductNameWithoutDot}VersionSuffix>{arcadeSuffix}</{this.ProductNameWithoutDot}VersionSuffix>
        <{this.ProductNameWithoutDot}VersionPatchNumber>{version.PatchNumber}</{this.ProductNameWithoutDot}VersionPatchNumber>
        <{this.ProductNameWithoutDot}VersionWithoutSuffix>{packageVersionWithoutSuffix}</{this.ProductNameWithoutDot}VersionWithoutSuffix>
        <{this.ProductNameWithoutDot}Version>{packageVersion}</{this.ProductNameWithoutDot}Version>
        <{this.ProductNameWithoutDot}AssemblyVersion>{assemblyVersion}</{this.ProductNameWithoutDot}AssemblyVersion>";
            }
            else
            {
                var packageSuffix = string.IsNullOrEmpty( version.VersionSuffix ) ? "" : "-" + version.VersionSuffix;
                packageVersion = packageVersionWithoutSuffix + packageSuffix;

                props += $@"
        <{this.ProductNameWithoutDot}Version>{packageVersion}</{this.ProductNameWithoutDot}Version>
        <{this.ProductNameWithoutDot}AssemblyVersion>{assemblyVersion}</{this.ProductNameWithoutDot}AssemblyVersion>";
            }

            props += $@"
        <{this.ProductNameWithoutDot}BuildConfiguration>{configuration}</{this.ProductNameWithoutDot}BuildConfiguration>
        <{this.ProductNameWithoutDot}Dependencies>{string.Join( ";", this.Dependencies.Select( x => x.Name ) )}</{this.ProductNameWithoutDot}Dependencies>
        <{this.ProductNameWithoutDot}PublicArtifactsDirectory>{this.PublicArtifactsDirectory}</{this.ProductNameWithoutDot}PublicArtifactsDirectory>
        <{this.ProductNameWithoutDot}PrivateArtifactsDirectory>{this.PrivateArtifactsDirectory}</{this.ProductNameWithoutDot}PrivateArtifactsDirectory>
        <{this.ProductNameWithoutDot}EngineeringVersion>{VersionHelper.EngineeringVersion}</{this.ProductNameWithoutDot}EngineeringVersion>
        <{this.ProductNameWithoutDot}VersionFilePath>{this.VersionsFilePath}</{this.ProductNameWithoutDot}VersionFilePath>
        <RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);$(MSBuildThisFileDirectory)</RestoreAdditionalProjectSources>
    </PropertyGroup>
    <ItemGroup>";

            foreach ( var dependency in dependenciesOverrideFile.Dependencies )
            {
                var buildSpec = dependency.Value.BuildServerSource;

                props += $@"
        <{this.ProductNameWithoutDot}Dependencies Include=""{dependency.Key}"">
            <SourceKind>{dependency.Value.SourceKind}</SourceKind>";

                if ( dependency.Value.Version != null )
                {
                    props += $@"
            <Version>{dependency.Value.Version}</Version>";
                }

                switch ( buildSpec )
                {
                    case CiBuildId buildId:
                        props += $@"
            <BuildNumber>{buildId.BuildNumber}</BuildNumber>
            <CiBuildTypeId>{buildId.BuildTypeId}</CiBuildTypeId>";

                        break;

                    case CiLatestBuildOfBranch branch:
                        props += props + $@"
            <Branch>{branch.Name}</Branch>";

                        break;
                }

                props
                    += $@"
        </{this.ProductNameWithoutDot}Dependencies>";
            }

            props += @"
    </ItemGroup>
    <PropertyGroup>
";

            foreach ( var dependency in dependenciesOverrideFile.Dependencies.Where( d => d.Value.SourceKind == DependencySourceKind.Feed ) )
            {
                var nameWithoutDot = dependency.Key.Replace( ".", "", StringComparison.OrdinalIgnoreCase );

                props += $@"
        <{nameWithoutDot}Version Condition=""'$({nameWithoutDot}Version)'==''"">{dependency.Value.Version}</{nameWithoutDot}Version>";
            }

            foreach ( var exportedProperty in mainVersionFileInfo.ExportedProperties )
            {
                props += $@"
        <{exportedProperty.Key} Condition=""'$({exportedProperty.Key}Version)'==''"">{exportedProperty.Value}</{exportedProperty.Key}>";
            }

            props += @"
    </PropertyGroup>
</Project>
";

            return props;
        }

        public void Clean( BuildContext context, BuildSettings settings )
        {
            void DeleteDirectory( string directory )
            {
                if ( Directory.Exists( directory ) )
                {
                    context.Console.WriteMessage( $"Deleting directory '{directory}'." );
                    Directory.Delete( directory, true );
                }
            }

            void CleanRecursive( string directory )
            {
                DeleteDirectory( Path.Combine( directory, "bin" ) );
                DeleteDirectory( Path.Combine( directory, "obj" ) );

                foreach ( var subdirectory in Directory.EnumerateDirectories( directory ) )
                {
                    if ( subdirectory == Path.Combine( context.RepoDirectory, this.EngineeringDirectory ) )
                    {
                        // Skip the engineering directory.
                        continue;
                    }

                    CleanRecursive( subdirectory );
                }
            }

            // Clears NuGet global-packages cache of Metalama and PostSharp.Engineering packages to prevent using old or corrupted package.
            void CleanNugetCache()
            {
                // Use dotnet command to locate nuget cache directory.
                ToolInvocationHelper.InvokeTool(
                    context.Console,
                    "dotnet",
                    "nuget locals global-packages -l",
                    context.RepoDirectory,
                    out _,
                    out var output );

                // Get only directory location string.
                var nugetCacheDirectory = output.Split( ' ' )[1].Trim();
                var directoryInfo = new DirectoryInfo( nugetCacheDirectory );

                // Delete all cached packages directories starting with 'Metalama'.
                foreach ( var dir in directoryInfo.EnumerateDirectories( "metalama*" ) )
                {
                    DeleteDirectory( Path.Combine( nugetCacheDirectory, dir.Name ) );
                }

                // Delete all cached packages directories starting with 'PostSharp.Engineering'.
                foreach ( var dir in directoryInfo.EnumerateDirectories( "postsharp.engineering*" ) )
                {
                    DeleteDirectory( Path.Combine( nugetCacheDirectory, dir.Name ) );
                }
            }

            // NugetCache gets automatically deleted only on TeamCity.
            if ( TeamCityHelper.IsTeamCityBuild( settings ) )
            {
                context.Console.WriteHeading( "Cleaning NuGet cache." );

                CleanNugetCache();
            }

            context.Console.WriteHeading( $"Cleaning {this.ProductName}." );

            foreach ( var directory in this.AdditionalDirectoriesToClean )
            {
                DeleteDirectory( Path.Combine( context.RepoDirectory, directory ) );
            }

            var stringParameters = new BuildInfo( null!, settings.BuildConfiguration, this );

            DeleteDirectory(
                Path.Combine(
                    context.RepoDirectory,
                    this.PrivateArtifactsDirectory.ToString( stringParameters ) ) );

            DeleteDirectory(
                Path.Combine(
                    context.RepoDirectory,
                    this.PublicArtifactsDirectory.ToString( stringParameters ) ) );

            DeleteDirectory(
                Path.Combine(
                    context.RepoDirectory,
                    this.LogsDirectory.ToString() ) );

            CleanRecursive( context.RepoDirectory );
        }

        private (string Private, string Public) GetArtifactsDirectories( BuildContext context, BuildInfo buildInfo )
        {
            return (
                Path.Combine( context.RepoDirectory, this.PrivateArtifactsDirectory.ToString( buildInfo ) ),
                Path.Combine( context.RepoDirectory, this.PublicArtifactsDirectory.ToString( buildInfo ) )
            );
        }

        public bool Verify( BuildContext context, PublishSettings settings )
        {
            var configuration = settings.BuildConfiguration;

            if ( configuration == BuildConfiguration.Public )
            {
                var versionFile = this.ReadGeneratedVersionFile( context.GetManifestFilePath( configuration ) );
                var directories = this.GetArtifactsDirectories( context, versionFile );

                // Verify that public packages have no private dependencies.
                if ( !VerifyPublicPackageCommand.Execute(
                        context.Console,
                        new VerifyPublicPackageCommandSettings { Directory = directories.Public } ) )
                {
                    return false;
                }

                return true;
            }
            else
            {
                context.Console.WriteError( "Artifacts can only be verified for the public build." );

                return false;
            }
        }

        public bool Publish( BuildContext context, PublishSettings settings )
        {
            var configuration = settings.BuildConfiguration;
            var versionFile = this.ReadGeneratedVersionFile( context.GetManifestFilePath( configuration ) );
            var directories = this.GetArtifactsDirectories( context, versionFile );

            var hasTarget = false;
            var configurationInfo = this.Configurations.GetValue( configuration );

            // Get the location of MainVersion.props file.
            var mainVersionFile = Path.Combine(
                context.RepoDirectory,
                this.MainVersionFilePath );

            // Get the current version from MainVersion.props.
            if ( !this.TryGetPreparedVersionInfo(
                    context,
                    mainVersionFile,
                    out var preparedVersionInfo ) )
            {
                return false;
            }

            // Only versioned products require version bump.
            if ( this.DependencyDefinition.IsVersioned )
            {
                // Analyze the repository state since the last deployment.
                if ( !TryAnalyzeGitHistory( context, out var hasBumpSinceLastDeployment, out var hasChangesSinceLastDeployment, out var lastVersionTag ) )
                {
                    return false;
                }

                // If there are no changes since the deployment, we get only a warning and deployment proceeds with the same version.
                if ( !hasChangesSinceLastDeployment )
                {
                    context.Console.WriteWarning( $"There are no new unpublished changes since the last deployment." );
                }
                else
                {
                    // To check if version was bumped manually we get full prepared version info.
                    var currentVersion = preparedVersionInfo.Version + preparedVersionInfo.PackageVersionSuffix;

                    // Publishing fails if there are changes and the version has not been bumped since the last deployment.
                    if ( !hasBumpSinceLastDeployment && currentVersion == lastVersionTag )
                    {
                        context.Console.WriteError( "There are changes since the last deployment but the version has not been bumped." );

                        return false;
                    }
                }
            }

            if ( configuration == BuildConfiguration.Public )
            {
                this.Verify( context, settings );
            }

            context.Console.WriteHeading( "Publishing files" );

            if ( !Publisher.PublishDirectory(
                    context,
                    settings,
                    directories,
                    configurationInfo,
                    versionFile,
                    false,
                    ref hasTarget ) )
            {
                return false;
            }

            if ( !Publisher.PublishDirectory(
                    context,
                    settings,
                    directories,
                    configurationInfo,
                    versionFile,
                    true,
                    ref hasTarget ) )
            {
                return false;
            }

            if ( !hasTarget )
            {
                context.Console.WriteWarning( "No active publishing target was detected." );
            }
            else
            {
                context.Console.WriteSuccess( "Publishing has succeeded." );
            }

            // Swap after successful publishing.
            if ( configurationInfo.SwapAfterPublishing )
            {
                context.Console.WriteMessage( "Swapping staging and production slots after publishing." );

                if ( !this.SwapAfterPublishing( context, settings ) )
                {
                    context.Console.WriteError( "Failed to swap after publishing." );

                    return false;
                }

                context.Console.WriteSuccess( "Swap after publishing has succeeded." );
            }

            // Only versioned products use version tags.
            if ( this.DependencyDefinition.IsVersioned )
            {
                // After successful artifact publishing the last commit is tagged with current version tag.
                if ( !AddTagToLastCommit( context, preparedVersionInfo, settings ) )
                {
                    context.Console.WriteError(
                        $"Could not tag the latest commit with version '{preparedVersionInfo.Version}{preparedVersionInfo.PackageVersionSuffix}'." );
                }
            }

            return true;
        }

        public bool SwapAfterPublishing( BuildContext context, PublishSettings publishSettings )
        {
            var swapSettings = new SwapSettings() { BuildConfiguration = publishSettings.BuildConfiguration, Dry = publishSettings.Dry };

            return this.Swap( context, swapSettings );
        }

        public bool Swap( BuildContext context, SwapSettings settings )
        {
            var configuration = this.Configurations.GetValue( settings.BuildConfiguration );
            var versionFile = this.ReadGeneratedVersionFile( context.GetManifestFilePath( settings.BuildConfiguration ) );
            var directories = this.GetArtifactsDirectories( context, versionFile );

            var success = true;

            if ( configuration.Swappers != null )
            {
                foreach ( var swapper in configuration.Swappers )
                {
                    switch ( swapper.Execute( context, settings, configuration ) )
                    {
                        case SuccessCode.Success:
                            foreach ( var tester in swapper.Testers )
                            {
                                switch ( tester.Execute( context, directories.Private, versionFile, configuration, settings.Dry ) )
                                {
                                    case SuccessCode.Success:
                                        break;

                                    // If any of the testers fail during swap, we do swap again to get the slots to their original state.
                                    case SuccessCode.Error:
                                        context.Console.WriteError(
                                            $"Tester failed after swapping staging and production slots. Attempting to revert the swap." );

                                        switch ( swapper.Execute( context, settings, configuration ) )
                                        {
                                            case SuccessCode.Success:
                                                context.Console.WriteMessage( "Successfully reverted swap." );

                                                break;

                                            case SuccessCode.Error:
                                                context.Console.WriteError( "Failed to revert swap." );

                                                break;

                                            case SuccessCode.Fatal:
                                                return false;
                                        }

                                        success = false;

                                        break;

                                    case SuccessCode.Fatal:
                                        return false;

                                    default:
                                        throw new NotImplementedException();
                                }
                            }

                            break;

                        case SuccessCode.Error:
                            success = false;

                            break;

                        case SuccessCode.Fatal:
                            return false;

                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            return success;
        }

        public bool BumpVersion( BuildContext context, BumpSettings settings )
        {
            context.Console.WriteHeading( $"Bumping the '{context.Product.ProductName}' version." );

            // If the version has already been dumped since the last deployment, there is nothing to do. 
            if ( !TryAnalyzeGitHistory( context, out var hasBumpSinceLastDeployment, out var hasChangesSinceLastDeployment, out _ ) )
            {
                return false;
            }

            if ( hasBumpSinceLastDeployment )
            {
                context.Console.WriteWarning( "Version has been bumped." );

                return true;
            }

            // Read the current version of the dependencies directly from source control.
            if ( !this.TryReadDependencyVersionsFromSourceRepos( context, true, out var dependencyVersions ) )
            {
                return false;
            }

            // Comparing the actual version of dependencies with the versions stored during the last bump.
            var newBumpInfoFile =
                new BumpInfoFile( dependencyVersions );

            var bumpInfoFilePath = Path.Combine(
                context.RepoDirectory,
                this.BumpInfoFilePath );

            if ( !File.Exists( bumpInfoFilePath ) )
            {
                context.Console.WriteError( $"File '{bumpInfoFilePath}' was not found." );

                return false;
            }

            var oldBumpFileContent = File.ReadAllText( bumpInfoFilePath );
            var hasChangesInDependencies = newBumpInfoFile.ToString() != oldBumpFileContent;

            if ( !hasChangesInDependencies && !hasChangesSinceLastDeployment )
            {
                context.Console.WriteWarning( $"There are no changes since the last deployment." );

                return true;
            }

            // If there is a change in dependencies versions, we update BumpInfo.txt with changes.
            if ( hasChangesInDependencies )
            {
                context.Console.WriteMessage(
                    $"'{bumpInfoFilePath}' contents are outdated. Overwriting its old content '{oldBumpFileContent}' with new content '{newBumpInfoFile}'." );

                File.WriteAllText( bumpInfoFilePath, newBumpInfoFile.ToString() );
            }

            Version? oldVersion;
            Version? newVersion;

            if ( this.MainVersionDependency == null )
            {
                if ( !this.BumpStrategy.TryBumpVersion( this, context, out oldVersion, out newVersion ) )
                {
                    return false;
                }
            }
            else
            {
                if ( hasChangesSinceLastDeployment && !hasChangesInDependencies )
                {
                    context.Console.WriteError(
                        "There are changes in the current repo but no changes in dependencies. However, the current repo does not have its own versioning. Do a fake change in a parent repo." );

                    return false;
                }

                var oldBumpInfo = BumpInfoFile.FromText( oldBumpFileContent );
                newVersion = dependencyVersions[this.MainVersionDependency.Name];
                oldVersion = oldBumpInfo?.Dependencies[this.MainVersionDependency.Name];
            }

            // Commit the version bump.
            if ( !this.TryCommitVersionBump( context, oldVersion, newVersion, settings ) )
            {
                return false;
            }

            return true;
        }

        private bool TryReadDependencyVersionsFromSourceRepos(
            BuildContext context,
            bool snapshotDependenciesOnly,
            [NotNullWhen( true )] out Dictionary<string, Version>? dependencyVersions )
        {
            dependencyVersions = new Dictionary<string, Version>();

            foreach ( var dependency in this.Dependencies.Union( this.SourceDependencies ) )
            {
                if ( snapshotDependenciesOnly && !dependency.GenerateSnapshotDependency )
                {
                    continue;
                }

                var mainVersionFile = Path.Combine( dependency.EngineeringDirectory, "MainVersion.props" );
                context.Console.WriteMessage( $"Downloading '{mainVersionFile}' from '{dependency.Repo.RepoUrl}'." );
                var mainVersionContent = dependency.Repo.DownloadTextFile( dependency.DefaultBranch, mainVersionFile );

                var document = XDocument.Parse( mainVersionContent );
                var project = Project.FromXmlReader( document.CreateReader(), new ProjectOptions() );
                var mainVersionPropertyValue = project.Properties.FirstOrDefault( p => p.Name == "MainVersion" )?.EvaluatedValue;

                if ( string.IsNullOrEmpty( mainVersionPropertyValue ) )
                {
                    context.Console.WriteError(
                        $"The property 'MainVersion' or its value in '{mainVersionFile}' of dependency '{dependency.Name}' is not defined." );

                    return false;
                }

                dependencyVersions.Add( dependency.Name, Version.Parse( mainVersionPropertyValue ) );
            }

            return true;
        }

        private bool GenerateTeamcityConfiguration( BuildContext context, string packageVersion )
        {
            var configurations = new[] { BuildConfiguration.Debug, BuildConfiguration.Release, BuildConfiguration.Public };

            var teamCityBuildConfigurations = new List<TeamCityBuildConfiguration>();

            foreach ( var configuration in configurations )
            {
                var configurationInfo = this.Configurations[configuration];
                var versionInfo = new BuildInfo( packageVersion, configuration, this );

                var publicArtifactsDirectory =
                    context.Product.PublicArtifactsDirectory.ToString( versionInfo ).Replace( "\\", "/", StringComparison.Ordinal );

                var privateArtifactsDirectory =
                    context.Product.PrivateArtifactsDirectory.ToString( versionInfo ).Replace( "\\", "/", StringComparison.Ordinal );

                var testResultsDirectory =
                    context.Product.TestResultsDirectory.ToString( versionInfo ).Replace( "\\", "/", StringComparison.Ordinal );

                var sourceDependenciesDirectory =
                    context.Product.SourceDependenciesDirectory.ToString().Replace( "\\", "/", StringComparison.Ordinal );

                var artifactRules =
                    $@"+:{publicArtifactsDirectory}/**/*=>{publicArtifactsDirectory}\n+:{privateArtifactsDirectory}/**/*=>{privateArtifactsDirectory}";

                // Add testResults to artifacts
                artifactRules += $@"\n+:{testResultsDirectory}/**/*=>{testResultsDirectory}";

                if ( context.Product.SourceDependencies.Length > 0 )
                {
                    artifactRules += $@"\n+:{sourceDependenciesDirectory}/**/*=>{sourceDependenciesDirectory}";
                }

                var additionalArtifactRules = this.DefaultArtifactRules;

                if ( configurationInfo.AdditionalArtifactRules != null )
                {
                    additionalArtifactRules = this.DefaultArtifactRules.AddRange( configurationInfo.AdditionalArtifactRules );
                }

                TeamCityBuildConfiguration? buildTeamCityConfiguration = null;

                if ( configurationInfo.ExportsToTeamCityBuild )
                {
                    buildTeamCityConfiguration = new TeamCityBuildConfiguration(
                        this,
                        objectName: $"{configuration}Build",
                        name: configurationInfo.TeamCityBuildName ?? $"Build [{configuration}]",
                        buildArguments: $"test --configuration {configuration} --buildNumber %build.number%",
                        buildAgentType: this.BuildAgentType )
                    {
                        RequiresClearCache = true,
                        ArtifactRules = artifactRules,
                        AdditionalArtifactRules = additionalArtifactRules.ToArray(),
                        BuildTriggers = configurationInfo.BuildTriggers,
                        SnapshotDependencyObjectNames = this.Dependencies?.Union( this.SourceDependencies )
                            .Where( d => d.GenerateSnapshotDependency )
                            .Select( d => d.CiBuildTypes[configuration] )
                            .ToArray()
                    };

                    teamCityBuildConfigurations.Add( buildTeamCityConfiguration );
                }

                TeamCityBuildConfiguration? teamCityDeploymentConfiguration = null;

                if ( buildTeamCityConfiguration != null && configurationInfo.ExportsToTeamCityDeploy )
                {
                    if ( configurationInfo.PrivatePublishers != null
                         || configurationInfo.PublicPublishers != null )
                    {
                        teamCityDeploymentConfiguration = new TeamCityBuildConfiguration(
                            this,
                            objectName: $"{configuration}Deployment",
                            name: configurationInfo.TeamCityDeploymentName ?? $"Deploy [{configuration}]",
                            buildArguments: $"publish --configuration {configuration}",
                            buildAgentType: this.BuildAgentType )
                        {
                            IsDeployment = true,
                            ArtifactDependencies = new[] { (buildTeamCityConfiguration.ObjectName, artifactRules) },
                            SnapshotDependencyObjectNames = this.Dependencies?.Union( this.SourceDependencies )
                                .Where( d => d.GenerateSnapshotDependency )
                                .Select( d => d.DeploymentBuildType )
                                .ToArray()
                        };

                        teamCityBuildConfigurations.Add( teamCityDeploymentConfiguration );
                    }
                }

                if (
                    buildTeamCityConfiguration != null
                    && configurationInfo.Swappers != null
                    && !configurationInfo.SwapAfterPublishing )
                {
                    teamCityBuildConfigurations.Add(
                        new TeamCityBuildConfiguration(
                            this,
                            objectName: $"{configuration}Swap",
                            name: configurationInfo.TeamCitySwapName ?? $"Swap [{configuration}]",
                            buildArguments: $"swap --configuration {configuration}",
                            buildAgentType: this.BuildAgentType )
                        {
                            IsDeployment = true,
                            SnapshotDependencyObjectNames = teamCityDeploymentConfiguration == null
                                ? null
                                : new[] { teamCityDeploymentConfiguration.ObjectName },
                            ArtifactDependencies = new[] { (buildTeamCityConfiguration.ObjectName, artifactRules) }
                        } );
                }
            }

            // Only versioned products can be bumped.
            if ( this.DependencyDefinition.IsVersioned )
            {
                var dependencyDefinitions = this.Dependencies;

                if ( dependencyDefinitions != null )
                {
                    teamCityBuildConfigurations.Add(
                        new TeamCityBuildConfiguration(
                            this,
                            objectName: "VersionBump",
                            name: $"Version Bump",
                            buildArguments: $"bump",
                            buildAgentType: this.BuildAgentType ) { IsDeployment = true } );
                }
            }

            var teamCityProject = new TeamCityProject( teamCityBuildConfigurations.ToArray() );
            var content = new StringWriter();
            teamCityProject.GenerateTeamcityCode( content );

            var filePath = Path.Combine( context.RepoDirectory, ".teamcity", "settings.kts" );

            if ( !File.Exists( filePath ) || File.ReadAllText( filePath ) != content.ToString() )
            {
                context.Console.WriteWarning( $"Replacing '{filePath}'." );
                File.WriteAllText( filePath, content.ToString() );
            }

            return true;
        }

        private static bool TryAnalyzeGitHistory(
            BuildContext context,
            out bool hasBumpSinceLastDeployment,
            out bool hasChangesSinceLastDeployment,
            [NotNullWhen( true )] out string? lastTagVersion )
        {
            lastTagVersion = null;

            // Fetch remote for tags and commits to make sure we have the full history to compare tags against.
            if ( !ToolInvocationHelper.InvokeTool(
                    context.Console,
                    "git",
                    "fetch",
                    context.RepoDirectory ) )
            {
                hasBumpSinceLastDeployment = false;
                hasChangesSinceLastDeployment = false;

                return false;
            }

            // Get string of the last published release tag matched by glob pattern and trim newline.
            var globMatch = "release/*";

            ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"describe --abbrev=0 --tags --match \"{globMatch}\"",
                context.RepoDirectory,
                out var exitCode,
                out var gitTagOutput );

            if ( exitCode != 0 )
            {
                hasBumpSinceLastDeployment = false;
                hasChangesSinceLastDeployment = false;

                context.Console.WriteError( gitTagOutput );

                context.Console.WriteError(
                    $"The repository may not have any tags matching pattern: '{globMatch}'. If so add 'release/0.0.0' tag to initial commit." );

                return false;
            }

            var lastTag = gitTagOutput.Trim();
            lastTagVersion = lastTag.Replace( "release/", "", StringComparison.OrdinalIgnoreCase );

            // Get commits log since the last deployment formatted to one line per commit.
            // ReSharper disable once StringLiteralTypo
            ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"log \"{lastTag}..HEAD\" --oneline",
                context.RepoDirectory,
                out exitCode,
                out var gitLogOutput );

            if ( exitCode != 0 )
            {
                hasBumpSinceLastDeployment = false;
                hasChangesSinceLastDeployment = false;

                context.Console.WriteError( gitTagOutput );

                return false;
            }

            // Check if we bumped since last deployment.
            hasBumpSinceLastDeployment = gitLogOutput.Contains( "VERSION_BUMP", StringComparison.OrdinalIgnoreCase );

            // Get count of commits since last deployment excluding version bumps and check if there are any changes.
            ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"rev-list --count \"{lastTag}..HEAD\" --invert-grep --grep=\"<<VERSION_BUMP>>\"",
                context.RepoDirectory,
                out exitCode,
                out var commitsCount );

            if ( exitCode != 0 )
            {
                hasBumpSinceLastDeployment = false;
                hasChangesSinceLastDeployment = false;

                context.Console.WriteError( gitTagOutput );

                return false;
            }

            var commitsSinceLastTag = int.Parse( commitsCount, CultureInfo.InvariantCulture );
            hasChangesSinceLastDeployment = commitsSinceLastTag > 0;

            return true;
        }

        private static bool AddTagToLastCommit( BuildContext context, PreparedVersionInfo preparedVersionInfo, BaseBuildSettings settings )
        {
            string versionTag;

            // The tag will contain 'release-' prefix if the version is intended for release and not only testing (i.e. -alpha, -beta suffixes).
            if ( preparedVersionInfo.PackageVersionSuffix == "-preview" || string.IsNullOrEmpty( preparedVersionInfo.PackageVersionSuffix ) )
            {
                versionTag = string.Concat( "release/", preparedVersionInfo.Version, preparedVersionInfo.PackageVersionSuffix );
            }
            else
            {
                versionTag = string.Concat( preparedVersionInfo.Version, preparedVersionInfo.PackageVersionSuffix );
            }

            ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"ls-remote --tags origin --grep {versionTag}",
                context.RepoDirectory,
                out _,
                out var gitOutput );

            if ( gitOutput.Contains( versionTag, StringComparison.OrdinalIgnoreCase ) )
            {
                context.Console.WriteWarning( $"Repository already contains tag '{versionTag}'." );

                return true;
            }

            // Tagging the last commit with version.
            if ( !ToolInvocationHelper.InvokeTool(
                    context.Console,
                    "git",
                    $"tag \"{versionTag}\"",
                    context.RepoDirectory ) )
            {
                return false;
            }

            // Returns the remote origin.
            if ( !ToolInvocationHelper.InvokeTool(
                    context.Console,
                    "git",
                    $"remote get-url origin",
                    context.RepoDirectory,
                    out _,
                    out var gitOrigin ) )
            {
                return false;
            }

            gitOrigin = gitOrigin.Trim();
            var isHttps = gitOrigin.StartsWith( "https", StringComparison.InvariantCulture );

            // When on TeamCity, if the repository is of HTTPS origin, the origin will be updated to form including Git authentication credentials.
            if ( TeamCityHelper.IsTeamCityBuild( settings ) )
            {
                if ( isHttps )
                {
                    if ( !TeamCityHelper.TryGetTeamCitySourceWriteToken(
                            out var teamcitySourceWriteTokenEnvironmentVariableName,
                            out var teamcitySourceCodeWritingToken ) )
                    {
                        context.Console.WriteImportantMessage(
                            $"{teamcitySourceWriteTokenEnvironmentVariableName} environment variable is not set. Using default credentials." );
                    }
                    else
                    {
                        gitOrigin = gitOrigin.Insert( 8, $"teamcity%40postsharp.net:{teamcitySourceCodeWritingToken}@" );
                    }
                }
            }

            // Pushes tag to origin.
            if ( !ToolInvocationHelper.InvokeTool(
                    context.Console,
                    "git",
                    $"push {gitOrigin} {versionTag}",
                    context.RepoDirectory ) )
            {
                return false;
            }

            context.Console.WriteSuccess( $"Tagging the latest commit with version '{versionTag}' was successful." );

            return true;
        }

        private bool TryCommitVersionBump( BuildContext context, Version? currentVersion, Version newVersion, CommonCommandSettings settings )
        {
            // Adds bumped MainVersion.props and updated BumpInfo.txt to Git staging area.
            if ( !ToolInvocationHelper.InvokeTool(
                    context.Console,
                    "git",
                    $"add {this.MainVersionFilePath} {this.BumpInfoFilePath}",
                    context.RepoDirectory ) )
            {
                return false;
            }

            // Returns the remote origin.
            ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"remote get-url origin",
                context.RepoDirectory,
                out var gitExitCode,
                out var gitOrigin );

            if ( gitExitCode != 0 )
            {
                context.Console.WriteError( gitOrigin );

                return false;
            }

            gitOrigin = gitOrigin.Trim();
            var isHttps = gitOrigin.StartsWith( "https", StringComparison.InvariantCulture );

            // When on TeamCity, Git user credentials are set to TeamCity and if the repository is of HTTPS origin, the origin will be updated to form including Git authentication credentials.
            if ( TeamCityHelper.IsTeamCityBuild( settings ) )
            {
                if ( !TeamCityHelper.TrySetGitIdentityCredentials( context ) )
                {
                    return false;
                }

                if ( isHttps )
                {
                    if ( !TeamCityHelper.TryGetTeamCitySourceWriteToken(
                            out var teamcitySourceWriteTokenEnvironmentVariableName,
                            out var teamcitySourceCodeWritingToken ) )
                    {
                        context.Console.WriteImportantMessage(
                            $"{teamcitySourceWriteTokenEnvironmentVariableName} environment variable is not set. Using default credentials." );
                    }
                    else
                    {
                        gitOrigin = gitOrigin.Insert( 8, $"teamcity%40postsharp.net:{teamcitySourceCodeWritingToken}@" );
                    }
                }
            }

            if ( !ToolInvocationHelper.InvokeTool(
                    context.Console,
                    "git",
                    $"commit -m \"<<VERSION_BUMP>> {currentVersion?.ToString() ?? "unknown"} to {newVersion}\"",
                    context.RepoDirectory ) )
            {
                return false;
            }

            if ( !ToolInvocationHelper.InvokeTool(
                    context.Console,
                    "git",
                    $"push {gitOrigin}",
                    context.RepoDirectory ) )
            {
                return false;
            }

            return true;
        }

        private record PreparedVersionInfo( Version Version, string PackageVersionSuffix );

        /// <summary>
        /// Reads the MyProduce.version.props file from the artifacts directory generated by the Prepare step.
        /// </summary>
        private bool TryGetPreparedVersionInfo(
            BuildContext context,
            string mainVersionFile,
            [NotNullWhen( true )] out PreparedVersionInfo? preparedVersionInfo )
        {
            var mainVersionFileInfo = this.ReadMainVersionFile( mainVersionFile );
            var overriddenPatchVersion = mainVersionFileInfo.OverriddenPatchVersion;

            string? mainVersion;
            Version? currentVersion;

            // The MainVersionDependency is not defined.
            if ( this.MainVersionDependency == null )
            {
                // The current version defaults to MainVersion.

                mainVersion = mainVersionFileInfo.MainVersion;

                currentVersion = Version.Parse( mainVersion );
            }
            else
            {
                // If MainVersionDependency and OverriddenPatchVersion properties are defined, we use OverriddenPatchVersion value.
                if ( !string.IsNullOrEmpty( overriddenPatchVersion ) )
                {
                    currentVersion = new Version( overriddenPatchVersion );
                }
                else
                {
                    // If no OverridenPatchVersion is defined, we use MainVersion property from private artifact.

                    var artifactVersionFile = Path.Combine(
                        context.RepoDirectory,
                        context.Product.PrivateArtifactsDirectory.ToString(),
                        context.Product.ProductName + ".version.props" );

                    var document = XDocument.Load( artifactVersionFile );
                    var project = document.Root;
                    var properties = project?.Element( "PropertyGroup" );
                    var propertyName = $"{context.Product.ProductNameWithoutDot}MainVersion";
                    mainVersion = properties?.Element( propertyName )?.Value;

                    if ( mainVersion == null )
                    {
                        context.Console.WriteError(
                            $"Cannot load '{mainVersionFile}': the property '{propertyName}' in '{artifactVersionFile}' is not defined." );

                        preparedVersionInfo = null;

                        return false;
                    }

                    // Set the current version to dependency version.
                    currentVersion = new Version( mainVersion );
                }
            }

            preparedVersionInfo = new PreparedVersionInfo( currentVersion, mainVersionFileInfo.PackageVersionSuffix );

            return true;
        }
    }
}