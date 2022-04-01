using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.FileSystemGlobbing;
using PostSharp.Engineering.BuildTools.Build.Publishers;
using PostSharp.Engineering.BuildTools.Build.Triggers;
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
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Project = Microsoft.Build.Evaluation.Project;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class Product
    {
        private readonly string? _versionsFile;

        private readonly string? _mainVersionFile;

        public Product()
        {
            this.BuildExePath = Assembly.GetCallingAssembly().Location;
        }

        public string BuildExePath { get; }

        public string EngineeringDirectory { get; init; } = "eng";

        public string VersionsFile
        {
            get => this._versionsFile ?? Path.Combine( this.EngineeringDirectory, "Versions.props" );
            init => this._versionsFile = value;
        }

        public string MainVersionFile
        {
            get => this._mainVersionFile ?? Path.Combine( this.EngineeringDirectory, "MainVersion.props" );
            init => this._mainVersionFile = value;
        }

        /// <summary>
        /// Gets the dependency from which the main version should be copied.
        /// </summary>
        public DependencyDefinition? MainVersionDependency { get; init; }

        public string ProductName { get; init; } = "Unnamed";

        public string ProductNameWithoutDot => this.ProductName.Replace( ".", "", StringComparison.OrdinalIgnoreCase );

        public ParametricString PrivateArtifactsDirectory { get; init; } = Path.Combine( "artifacts", "publish", "private" );

        public ParametricString PublicArtifactsDirectory { get; init; } = Path.Combine( "artifacts", "publish", "public" );

        public bool GenerateArcadeProperties { get; init; }

        public string[] AdditionalDirectoriesToClean { get; init; } = Array.Empty<string>();

        public Solution[] Solutions { get; init; } = Array.Empty<Solution>();

        public Pattern PrivateArtifacts { get; init; } = Pattern.Empty;

        public Pattern PublicArtifacts { get; init; } = Pattern.Empty;

        public bool KeepEditorConfig { get; init; }

        public string BuildAgentType { get; init; } = "caravela02";

        public ConfigurationSpecific<BuildConfigurationInfo> Configurations { get; init; } = DefaultConfigurations;

        public static ConfigurationSpecific<BuildConfigurationInfo> DefaultConfigurations { get; }
            = new(
                debug: new BuildConfigurationInfo( MSBuildName: "Debug", BuildTriggers: new IBuildTrigger[] { new SourceBuildTrigger() } ),
                release: new BuildConfigurationInfo( MSBuildName: "Release", RequiresSigning: true ),
                @public: new BuildConfigurationInfo(
                    MSBuildName: "Release",
                    RequiresSigning: true,
                    PublicPublishers: new Publisher[]
                    {
                        new NugetPublisher( Pattern.Create( "*.nupkg" ), "https://api.nuget.org/v3/index.json", "%NUGET_ORG_API_KEY%" ),
                        new VsixPublisher( Pattern.Create( "*.vsix" ) )
                    } ) );

        /// <summary>
        /// Gets the set of dependencies of this product. Some commands expect the dependency to exist in <see cref="PostSharp.Engineering.BuildTools.Dependencies.Model.Dependencies.All"/>.
        /// </summary>
        public DependencyDefinition[] Dependencies { get; init; } = Array.Empty<DependencyDefinition>();

        public DependencyDefinition? GetDependency( string name )
            => this.Dependencies.SingleOrDefault( d => d.Name == name )
               ?? BuildTools.Dependencies.Model.Dependencies.All.SingleOrDefault( d => d.Name == name );

        public Dictionary<string, string> SupportedProperties { get; init; } = new();

        public bool RequiresEngineeringSdk { get; init; } = true;

        public bool Build( BuildContext context, BuildSettings settings )
        {
            var configuration = settings.BuildConfiguration;
            var buildConfigurationInfo = this.Configurations[configuration];

            // The build for public configuration will not run if there are no changes since the last version tag.
            if ( configuration == BuildConfiguration.Public )
            {
                if ( !settings.Force )
                {
                    // Get the latest version tag.
                    TryGetLastVersionTag( context, out var versionTag );

                    // If there are no changes since the last tag (i.e. last publishing) the build will end successfully here.
                    if ( !AreChangesSinceLastVersionTag( context, versionTag ) )
                    {
                        return true;
                    }
                }
            }

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

                // Sign public artifacts.
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
                                $"Sign --baseDirectory \"{publicArtifactsDirectory}\" --input {filter} --config $(ToolsDirectory)\\signclient-appsettings.json --name {this.ProductName} --user sign-caravela@postsharp.net --secret %SIGNSERVER_SECRET%" );
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
                if ( !VersionsOverrideFile.TryLoad( context, configuration, out var versionsOverrideFile ) )
                {
                    return false;
                }

                foreach ( var dependency in versionsOverrideFile.Dependencies )
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

        private static (string MainVersion, string? PatchVersion, string PackageVersionSuffix) ReadMainVersionFile( string path )
        {
            var versionFilePath = path;
            var versionFile = Project.FromFile( versionFilePath, new ProjectOptions() );

            var mainVersion = versionFile
                .Properties
                .SingleOrDefault( p => p.Name == "MainVersion" )
                ?.EvaluatedValue;

            var patchVersion = versionFile
                .Properties
                .SingleOrDefault( p => p.Name == "PatchVersion" )
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

            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

            return (mainVersion, patchVersion, suffix);
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
            foreach ( var solution in this.Solutions )
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

                    switch ( buildMethod )
                    {
                        case BuildMethod.Build:
                            if ( !solution.Build( context, settings ) )
                            {
                                return false;
                            }

                            break;

                        case BuildMethod.Pack:
                            if ( solution.PackRequiresExplicitBuild && !settings.NoDependencies )
                            {
                                if ( !solution.Build( context, settings ) )
                                {
                                    return false;
                                }
                            }

                            if ( !solution.Pack( context, settings ) )
                            {
                                return false;
                            }

                            break;

                        case BuildMethod.Test:
                            if ( !solution.Test( context, settings ) )
                            {
                                return false;
                            }

                            break;

                        default:
                            throw new NotImplementedException( $"Build method '{buildMethod}' is not implemented." );
                    }

                    context.Console.WriteSuccess( $"Building {solution.Name} was successful." );
                }
            }

            return true;
        }

        public bool Test( BuildContext context, BuildSettings settings )
        {
            if ( !settings.NoDependencies && !this.Build( context, settings.WithIncludeTests( true ) ) )
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

            foreach ( var solution in this.Solutions )
            {
                var solutionOptions = settings;

                if ( settings.AnalyzeCoverage && solution.SupportsTestCoverage )
                {
                    solutionOptions =
                        settings.WithAdditionalProperties( properties ).WithoutConcurrency();
                }

                context.Console.WriteHeading( $"Testing {solution.Name}." );

                if ( !solution.Test( context, solutionOptions ) )
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
            var configuration = settings.BuildConfiguration;

            if ( !settings.NoDependencies )
            {
                this.Clean( context, settings );
            }

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
            if ( !VersionsOverrideFile.TryLoad( context, configuration, out var versionsOverrideFile ) )
            {
                return false;
            }

            // If we have any non-feed dependency that does not have a resolved VersionFile, it means that we have not fetched yet. 
            if ( versionsOverrideFile.Dependencies.Any( d => d.Value.SourceKind != DependencySourceKind.Feed && d.Value.VersionFile == null ) )
            {
                FetchDependencyCommand.FetchDependencies( context, configuration, versionsOverrideFile );

                versionsOverrideFile.LocalBuildFile = propsFilePath;
            }

            // We always save the Versions.g.props because it may not exist and it may have been changed by the previous step.
            versionsOverrideFile.LocalBuildFile = propsFilePath;

            if ( !versionsOverrideFile.TrySave( context ) )
            {
                return false;
            }

            // Read the main version number.
            if ( !this.TryComputeVersion( context, settings, configuration, versionsOverrideFile, out var version ) )
            {
                return false;
            }

            // Generate Versions.g.props.
            var props = this.GenerateVersionFile( version, configuration, versionsOverrideFile, out var packageVersion );
            context.Console.WriteMessage( $"Writing '{propsFilePath}'." );
            File.WriteAllText( propsFilePath, props );

            // Generating the configuration-neutral Versions.g.props for the prepared configuration.
            var configurationNeutralVersionsFilePath = this.GetConfigurationNeutralVersionsFilePath( context );

            context.Console.WriteMessage( $"Writing '{configurationNeutralVersionsFilePath}'." );

            File.WriteAllText(
                configurationNeutralVersionsFilePath,
                $@"
<Project>
    <PropertyGroup>
        <EngineeringConfiguration>{settings.BuildConfiguration}</EngineeringConfiguration>
    </PropertyGroup>
    <Import Project=""Versions.{settings.BuildConfiguration}.g.props"" />
</Project>
" );

            // Generating the TeamCity file.
            if ( !this.GenerateTeamcityConfiguration( context, packageVersion ) )
            {
                return false;
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
                $"Preparing the build was successful. {this.ProductNameWithoutDot}Version={this.ReadGeneratedVersionFile( context.GetManifestFilePath( configuration ) ).PackageVersion}" );

            return true;
        }

        private bool TryComputeVersion(
            BuildContext context,
            BuildSettings settings,
            BuildConfiguration configuration,
            VersionsOverrideFile versionsOverrideFile,
            [NotNullWhen( true )] out VersionComponents? version )
        {
            var configurationLowerCase = configuration.ToString().ToLowerInvariant();

            version = null;
            string? mainVersion;
            string? mainPackageVersionSuffix;
            string? patchVersion;

            (mainVersion, patchVersion, mainPackageVersionSuffix) =
                ReadMainVersionFile(
                    Path.Combine(
                        context.RepoDirectory,
                        this.EngineeringDirectory,
                        "MainVersion.props" ) );

            if ( this.MainVersionDependency != null )
            {
                // The main version is defined in a dependency. Load the import file.

                if ( !versionsOverrideFile.Dependencies.TryGetValue( this.MainVersionDependency.Name, out var dependencySource ) )
                {
                    context.Console.WriteError( $"Cannot find a dependency named '{this.MainVersionDependency.Name}'." );

                    return false;
                }
                else if ( dependencySource.VersionFile == null )
                {
                    context.Console.WriteError( $"The dependency '{this.MainVersionDependency.Name}' is not resolved." );

                    return false;
                }

                var versionFile = Project.FromFile( dependencySource.VersionFile, new ProjectOptions() );

                var propertyName = this.MainVersionDependency!.NameWithoutDot + "MainVersion";

                mainVersion = versionFile.Properties.SingleOrDefault( p => p.Name == propertyName )
                    ?.UnevaluatedValue;

                if ( string.IsNullOrWhiteSpace( mainVersion ) )
                {
                    context.Console.WriteError( $"The file '{dependencySource.VersionFile}' does not contain the {propertyName}." );

                    return false;
                }

                // Note that the version suffix is not copied from the dependency, only the main version. 

                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            }

            if ( !string.IsNullOrWhiteSpace( patchVersion ) && !patchVersion.StartsWith( mainVersion + ".", StringComparison.Ordinal ) )
            {
                context.Console.WriteError(
                    $"The PatchVersion property in MainVersion.props ({patchVersion}) does not match the MainVersion property value ({mainVersion})." );

                return false;
            }

            var versionPrefix = mainVersion;
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
                    versionSuffix = mainPackageVersionSuffix.TrimStart( '-' );
                    patchNumber = 0;

                    if ( !string.IsNullOrWhiteSpace( patchVersion ) )
                    {
                        var parsedPatchVersion = Version.Parse( patchVersion );
                        patchNumber = parsedPatchVersion.Revision;
                    }

                    break;

                default:
                    throw new InvalidOperationException();
            }

            version = new VersionComponents( mainVersion, versionPrefix, patchNumber, versionSuffix );

            return true;
        }

        private string GenerateVersionFile(
            VersionComponents version,
            BuildConfiguration configuration,
            VersionsOverrideFile versionsOverrideFile,
            out string packageVersion )
        {
            var props = $@"
<!-- This file is generated by the engineering tooling -->
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
        <{this.ProductNameWithoutDot}VersionFilePath>{this.VersionsFile}</{this.ProductNameWithoutDot}VersionFilePath>
        <RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);$(MSBuildThisFileDirectory)</RestoreAdditionalProjectSources>
    </PropertyGroup>
    <ItemGroup>";

            foreach ( var dependency in versionsOverrideFile.Dependencies )
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

            foreach ( var dependency in versionsOverrideFile.Dependencies.Where( d => d.Value.SourceKind == DependencySourceKind.Feed ) )
            {
                var nameWithoutDot = dependency.Key.Replace( ".", "", StringComparison.OrdinalIgnoreCase );

                props += $@"
        <{nameWithoutDot}Version Condition=""'$({nameWithoutDot}Version)'==''"">{dependency.Value.Version}</{nameWithoutDot}Version>";
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
                this.MainVersionFile );

            // Get the current version from MainVersion.props.
            if ( !TryLoadMainVersion( mainVersionFile, out var currentVersion, out var packageVersionSuffix ) )
            {
                return false;
            }
            
            // Get the latest version tag.
            if ( !TryGetLastVersionTag( context, out var lastVersionTag ) )
            {
                return false;
            }

            // Using --force flag ignores checks for changes and version bump.
            if ( !settings.Force )
            {
                // If there are no changes since the last tag (i.e. last publishing) the publishing will end successfully here.
                if ( !AreChangesSinceLastVersionTag( context, lastVersionTag ) )
                {
                    return true;
                }

                // If version has not been bumped since the last publish, it requires manual bump and therefore the version can't be published.
                if ( !IsVersionBumped( context, currentVersion, lastVersionTag ) )
                {
                    return false;
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

            // TODO: should following 3 steps affect publishing (hence the 'falses')
            // After successful artifact publishing the last commit is tagged with current version tag.
            if ( !AddTagToLastCommit( context, currentVersion, packageVersionSuffix ) )
            {
                return false;
            }

            // TODO: Should merge fail affect the Publishing?
            // Checks if the current branch needs to be merged to master.
            if ( RequiresMergeOfBranches( context, out var currentBranch ) )
            {
                context.Console.WriteImportantMessage( $"Branch '{currentBranch}' requires merging to master." );

                // If so, merge is started.
                if ( !MergeBranchToMaster( context, currentBranch ) )
                {
                    return false;
                }
            }
            
            // TODO: Shouldn't commit tagging be after merge commit?

            // Finally the MainVersion.props version is bumped.
            if ( !this.BumpVersion( context, mainVersionFile, currentVersion, packageVersionSuffix ) )
            {
                return false;
            }

            return true;
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

                                    case SuccessCode.Error:
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

                var artifactRules =
                    $@"+:{publicArtifactsDirectory}/**/*=>{publicArtifactsDirectory}\n+:{privateArtifactsDirectory}/**/*=>{privateArtifactsDirectory}";

                var buildTeamCityConfiguration = new TeamCityBuildConfiguration(
                    objectName: $"{configuration}Build",
                    name: configurationInfo.TeamCityBuildName ?? $"Build [{configuration}]",
                    buildArguments: $"test --configuration {configuration} --buildNumber %build.number%",
                    buildAgentType: this.BuildAgentType )
                {
                    ArtifactRules = artifactRules,
                    AdditionalArtifactRules = configurationInfo.AdditionalArtifactRules,
                    BuildTriggers = configurationInfo.BuildTriggers,
                    SnapshotDependencyObjectNames = this.Dependencies?
                        .Where( d => d.Provider != VcsProvider.None && d.GenerateSnapshotDependency )
                        .Select( d => d.CiBuildTypes[configuration] )
                        .ToArray()
                };

                teamCityBuildConfigurations.Add( buildTeamCityConfiguration );

                TeamCityBuildConfiguration? teamCityDeploymentConfiguration = null;

                if ( configurationInfo.PrivatePublishers != null
                     || configurationInfo.PublicPublishers != null )
                {
                    teamCityDeploymentConfiguration = new TeamCityBuildConfiguration(
                        objectName: $"{configuration}Deployment",
                        name: configurationInfo.TeamCityDeploymentName ?? $"Deploy [{configuration}]",
                        buildArguments: $"publish --configuration {configuration}",
                        buildAgentType: this.BuildAgentType )
                    {
                        IsDeployment = true, ArtifactDependencies = new[] { (buildTeamCityConfiguration.ObjectName, artifactRules) }
                    };

                    teamCityBuildConfigurations.Add( teamCityDeploymentConfiguration );
                }

                if ( configurationInfo.Swappers != null )
                {
                    teamCityBuildConfigurations.Add(
                        new TeamCityBuildConfiguration(
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

        private static bool TryGetLastVersionTag( BuildContext context, [NotNullWhen( true )] out string? lastVersionTag )
        {
            // Returns the list of tag reference names treated as versions in descending order.
            ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                "tag --sort=-version:refname",
                context.RepoDirectory,
                out _,
                out var tags );

            // Only the most recent tag from the list of is used.
            using ( var reader = new StringReader( tags ) )
            {
                lastVersionTag = reader.ReadLine();
            }

            if ( string.IsNullOrEmpty( lastVersionTag ) )
            {
                context.Console.WriteWarning( "There is no version tag in this repository. For clean repositories tag the initial commit with 0.0.0 version tag." );

                return false;
            }

            return true;
        }
        
        private static bool AreChangesSinceLastVersionTag( BuildContext context, string? lastVersionTag )
        {
            // Gets the count from list of committed changes between last version tag and current HEAD excluding version bumps.
            ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"rev-list --count \"{lastVersionTag}..HEAD\" --invert-grep --grep=\"<<VERSION_BUMP>>\"",
                context.RepoDirectory,
                out var gitExitCode,
                out var gitOutput );

            if ( gitExitCode != 0 )
            {
                context.Console.WriteError( gitOutput );

                return false;
            }

            var commitsCount = int.Parse( gitOutput, CultureInfo.InvariantCulture );

            if ( commitsCount > 0 )
            {
                context.Console.WriteWarning( $"There is total of {commitsCount} unpublished commits since '{lastVersionTag}' tag." );

                return true;
            }

            context.Console.WriteWarning( "There are no unpublished changes since the last version tag. Use --force." );

            return false;
        }

        private static bool IsVersionBumped( BuildContext context, Version currentVersion, string lastVersionTag )
        {
            var version = lastVersionTag;

            // Version can contain suffixes such as "-preview". By convention, all our version numbers before the dash are unique (i.e. given a version x.y-z1, we never have x.y-z2).
            if ( version.Contains( '-', StringComparison.InvariantCulture ) )
            {
                // Only numeric part of version is kept.
                version = lastVersionTag.Substring( 0, lastVersionTag.IndexOf( '-', StringComparison.InvariantCulture ) );
            }

            var lastVersion = new Version( version );

            if ( lastVersion > currentVersion )
            {
                context.Console.WriteError( $"Last tag version '{lastVersion}' is bigger than current version '{currentVersion}'." );

                return false;
            }

            if ( lastVersion == currentVersion )
            {
                context.Console.WriteError( $"The '{context.Product.ProductName}' version has not been bumped. Use --force." );

                return false;
            }

            return true;
        }

        private static bool AddTagToLastCommit( BuildContext context, Version? version, string? packageVersionSuffix )
        {
            var versionTag = string.Concat( version, packageVersionSuffix );

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
            if ( TeamCityHelper.IsTeamCityBuild )
            {
                if ( isHttps )
                {
                    if ( !TeamCityHelper.TryGetTeamCitySourceWriteToken( out var teamcitySourceWriteTokenEnvironmentVariableName, out var teamcitySourceCodeWritingToken ) )
                    {
                        context.Console.WriteImportantMessage( $"{teamcitySourceWriteTokenEnvironmentVariableName} environment variable is not set. Using default credentials." );
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

        private static bool RequiresMergeOfBranches( BuildContext context, [NotNullWhen( true )] out string? currentBranch )
        {
            // TODO: Keep this?
            // Fetch all remotes to make sure the merge has not already been done.
            ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"fetch --all",
                context.RepoDirectory );

            // Returns the reference name of the current branch.
            ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"branch --show-current",
                context.RepoDirectory,
                out var gitExitCode,
                out var gitOutput );

            if ( gitExitCode != 0 )
            {
                context.Console.WriteError( gitOutput );
                currentBranch = null;

                return false;
            }

            currentBranch = gitOutput.Trim();

            // Returns the last commit on the current branch in the commit hash format.
            ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"log -n 1 --pretty=format:\"%H\"",
                context.RepoDirectory,
                out gitExitCode,
                out gitOutput );
            
            if ( gitExitCode != 0 )
            {
                context.Console.WriteError( gitOutput );

                return false;
            }

            var lastCurrentBranchCommitHash = gitOutput;

            // Returns hash of as good common ancestor commit as possible between master and current branch.
            ToolInvocationHelper.InvokeTool(
                context.Console,
                "git",
                $"merge-base master {currentBranch}",
                context.RepoDirectory,
                out gitExitCode,
                out gitOutput );
            
            if ( gitExitCode != 0 )
            {
                context.Console.WriteError( gitOutput );

                return false;
            }

            var lastCommonCommitHash = gitOutput;

            // If the commit hashes are equal, there haven't been any unmerged commits.
            return !lastCurrentBranchCommitHash.Equals( lastCommonCommitHash, StringComparison.Ordinal );
        }

        private static bool MergeBranchToMaster( BuildContext context, string branchToMerge )
        {
            // Change to the master branch before we do merge.
            if ( !ToolInvocationHelper.InvokeTool(
                    context.Console, 
                    "git",
                    $"checkout master",
                    context.RepoDirectory ) )
            {
                return false;
            }

            // TODO: decide if --no-ff is kept or not, the merge commit message is not created if FastForward merge is occurring.
            // Attempts merging branch to master with custom merge message. --no-ff option is to force merge commit to be created.
            if ( !ToolInvocationHelper.InvokeTool(
                    context.Console,
                    "git",
                    $"merge {branchToMerge} --no-ff -m \"Merged '{branchToMerge}' to 'master'.\"",
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
                out var gitOutput );

            if ( gitExitCode != 0 )
            {
                context.Console.WriteError( gitOutput );
                
                return false;
            }

            var gitOrigin = gitOutput.Trim();

            var isHttps = gitOrigin.StartsWith( "https", StringComparison.InvariantCulture );

            // When on TeamCity, origin will be updated to form including Git authentication credentials.
            if ( TeamCityHelper.IsTeamCityBuild )
            {
                if ( isHttps )
                {
                    if ( !TeamCityHelper.TryGetTeamCitySourceWriteToken( out var teamcitySourceWriteTokenEnvironmentVariableName, out var teamcitySourceCodeWritingToken ) )
                    {
                        context.Console.WriteImportantMessage( $"{teamcitySourceWriteTokenEnvironmentVariableName} environment variable is not set. Using default credentials." );
                    }
                    else
                    {
                        gitOrigin = gitOrigin.Insert( 8, $"teamcity%40postsharp.net:{teamcitySourceCodeWritingToken}@" );
                    }
                }
            }

            // Push completed merge operation to remote.
            if ( !ToolInvocationHelper.InvokeTool(
                    context.Console, 
                    "git",
                    $"push {gitOrigin}",
                    context.RepoDirectory ) )
            {
                return false;
            }

            context.Console.WriteSuccess( $"Merging '{branchToMerge}' into 'master' branch was successful." );

            return true;
        }

        private bool BumpVersion( BuildContext context, string mainVersionFile, Version currentVersion, string packageVersionSuffix )
        {
            if ( !File.Exists( mainVersionFile ) )
            {
                context.Console.WriteError( $"The file '{mainVersionFile}' does not exist." );

                return false;
            }

            // Increment the version.
            if ( !IncrementVersion( context, currentVersion, out var newVersion ) )
            {
                context.Console.WriteError( $"Could not increment version '{currentVersion}'." );

                return false;
            }

            // Save the MainVersion.props with new version.
            if ( !TrySaveMainVersion( context, mainVersionFile, newVersion, packageVersionSuffix ) )
            {
                return false;
            }

            // Commit the version bump.
            if ( !this.TryCommitVersionBump( context, currentVersion, newVersion ) )
            {   
                return false;
            }

            context.Console.WriteSuccess( $"Bumping the '{context.Product.ProductName}' version from '{currentVersion}{packageVersionSuffix}' to '{newVersion}{packageVersionSuffix}' was successful." );

            return true;
        }

        private bool TryCommitVersionBump( BuildContext context, Version currentVersion, Version newVersion )
        {
            // Adds bumped MainVersion.props to Git staging area.
            if ( !ToolInvocationHelper.InvokeTool(
                    context.Console,
                    "git",
                    $"add {this.MainVersionFile}",
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
            if ( TeamCityHelper.IsTeamCityBuild )
            {
                // Following configurations are set only for the current operations in the repository.
                if ( !ToolInvocationHelper.InvokeTool(
                        context.Console,
                        "git",
                        "config user.name TeamCity",
                        context.RepoDirectory ) )
                {
                    return false;
                }

                if ( !ToolInvocationHelper.InvokeTool(
                        context.Console,
                        "git",
                        "config user.email teamcity@postsharp.net",
                        context.RepoDirectory ) )
                {
                    return false;
                }

                if ( isHttps )
                {
                    if ( !TeamCityHelper.TryGetTeamCitySourceWriteToken( out var teamcitySourceWriteTokenEnvironmentVariableName, out var teamcitySourceCodeWritingToken ) )
                    {
                        context.Console.WriteImportantMessage( $"{teamcitySourceWriteTokenEnvironmentVariableName} environment variable is not set. Using default credentials." );
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
                    $"commit -m \"<<VERSION_BUMP>> {currentVersion} to {newVersion}\"",
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

        private static bool IncrementVersion( BuildContext context, Version currentVersion, out Version newVersion )
        {
            var major = currentVersion.Major;
            var minor = currentVersion.Minor;
            var build = currentVersion.Build;

            build++;

            newVersion = new Version( major, minor, build );

            return true;
        }

        private static bool TryLoadMainVersion( string mainVersionFile, [NotNullWhen( true )] out Version? currentVersion, out string packageVersionSuffix )
        {
            var document = XDocument.Load( mainVersionFile );
            var project = document.Root;
            var properties = project?.Element( "PropertyGroup" );
            var mainVersionString = properties?.Element( "MainVersion" )?.Value;
            packageVersionSuffix = properties?.Element( "PackageVersionSuffix" )?.Value ?? string.Empty;

            if ( mainVersionString == null )
            {
                currentVersion = null;

                return false;
            }

            currentVersion = Version.Parse( mainVersionString );

            return true;
        }

        private static bool TrySaveMainVersion( BuildContext context, string mainVersionFile, Version version, string packageVersionSuffix )
        {
            if ( !File.Exists( mainVersionFile ) )
            {
                context.Console.WriteError( $"Could not save '{mainVersionFile}' with version '{version}'." );

                return false;
            }

            var document = XDocument.Load( mainVersionFile );
            var project = document.Root;
            var properties = project!.Element( "PropertyGroup" );
            var mainVersionElement = properties!.Element( "MainVersion" );
            var packageVersionSuffixElement = properties?.Element( "PackageVersionSuffix" );
            
            mainVersionElement!.Value = version.ToString();
            packageVersionSuffixElement!.Value = packageVersionSuffix;

            // Using settings to keep the indentation as well as encoding identical to original MainVersion.props.
            var xmlWriterSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, IndentChars = "    ", Encoding = new UTF8Encoding( false ) };

            using ( var xmlWriter = XmlWriter.Create( mainVersionFile, xmlWriterSettings ) )
            {
                document.Save( xmlWriter );
            }

            context.Console.WriteMessage( $"Writing '{mainVersionFile}'." );

            return true;
        }
    }
}