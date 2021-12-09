using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Extensions.FileSystemGlobbing;
using PostSharp.Engineering.BuildTools.Coverage;
using PostSharp.Engineering.BuildTools.Dependencies;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.NuGet;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class Product
    {
        private readonly string? _versionsFile;

        public string EngineeringDirectory { get; init; } = "eng";

        public string VersionsFile
        {
            get => this._versionsFile ?? Path.Combine( this.EngineeringDirectory, "Versions.props" );
            init => this._versionsFile = value;
        }

        public string ProductName { get; init; } = "Unnamed";

        public string ProductNameWithoutDot => this.ProductName.Replace( ".", "", StringComparison.OrdinalIgnoreCase );

        public ParametricString PrivateArtifactsDirectory { get; init; } = Path.Combine( "artifacts", "publish", "private" );

        public ParametricString PublicArtifactsDirectory { get; init; } = Path.Combine( "artifacts", "publish", "public" );

        public bool GenerateArcadeProperties { get; init; }

        public ImmutableArray<string> AdditionalDirectoriesToClean { get; init; } = ImmutableArray<string>.Empty;

        public ImmutableArray<Solution> Solutions { get; init; } = ImmutableArray<Solution>.Empty;

        public Pattern PrivateArtifacts { get; init; } = Pattern.Empty;

        public Pattern PublicArtifacts { get; init; } = Pattern.Empty;

        public bool KeepEditorConfig { get; init; }

        /// <summary>
        /// Set of dependencies of this product. Some commands expect the dependency to exist in <see cref="DependencyDefinition.All"/>
        /// </summary>
        public ImmutableArray<DependencyDefinition> Dependencies { get; init; } = ImmutableArray<DependencyDefinition>.Empty;

        public ImmutableDictionary<string, string> SupportedProperties { get; init; } =
            ImmutableDictionary<string, string>.Empty;

        public bool RequiresEngineeringSdk { get; init; } = true;

        public bool Build( BuildContext context, BuildSettings settings )
        {
            // Validate options.
            if ( settings.PublicBuild )
            {
                if ( settings.BuildConfiguration != BuildConfiguration.Release )
                {
                    context.Console.WriteError( $"Cannot build a public version of a {settings.BuildConfiguration} build without --force." );

                    return false;
                }
            }

            // Build dependencies.
            if ( !settings.NoDependencies && !this.Prepare( context, settings ) )
            {
                return false;
            }

            // Delete the root import file in the repo because the presence of this file means a successful build.
            this.DeleteImportFile( context, settings.BuildConfiguration );

            // We have to read the version from the file we have generated - using MSBuild, because it contains properties.
            var versionInfo = this.ReadGeneratedVersionFile( context.GetManifestFilePath( settings.BuildConfiguration ) );

            var privateArtifactsDir = Path.Combine(
                context.RepoDirectory,
                this.PrivateArtifactsDirectory.ToString( versionInfo ) );

            // Build.
            if ( !this.BuildCore( context, settings ) )
            {
                return false;
            }

            // Allow for some customization before we create the zip file and copy to the public directory.
            this.BuildCompleted?.Invoke( (context, settings, privateArtifactsDir) );

            // Check that the build produced the expected artifacts.
            var allFilesPattern = this.PublicArtifacts.Add( this.PrivateArtifacts );

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

            // If we're doing a public build, copy public artifacts to the publish directory.
            if ( settings.PublicBuild )
            {
                // Copy artifacts.
                context.Console.WriteHeading( "Copying public artifacts" );
                var files = new List<FilePatternMatch>();

                this.PublicArtifacts.TryGetFiles( privateArtifactsDir, versionInfo, files );

                var publicArtifactsDirectory = Path.Combine(
                    context.RepoDirectory,
                    this.PublicArtifactsDirectory.ToString( versionInfo ) );

                if ( !Directory.Exists( publicArtifactsDirectory ) )
                {
                    Directory.CreateDirectory( publicArtifactsDirectory );
                }

                foreach ( var file in files )
                {
                    var targetFile = Path.Combine( publicArtifactsDirectory, Path.GetFileName( file.Path ) );

                    context.Console.WriteMessage( file.Path );
                    File.Copy( Path.Combine( privateArtifactsDir, file.Path ), targetFile, true );
                }

                // Verify that public packages have no private dependencies.
                if ( !VerifyPublicPackageCommand.Execute(
                    context.Console,
                    new VerifyPackageSettings { Directory = publicArtifactsDirectory } ) )
                {
                    return false;
                }

                // Sign public artifacts.
                var signSuccess = true;

                if ( settings.Sign )
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
                                $"Sign --baseDirectory {publicArtifactsDirectory} --input {filter} --config $(ToolsDirectory)\\signclient-appsettings.json --name {this.ProductName} --user sign-Metalama@postsharp.net --secret %SIGNSERVER_SECRET%",
                                context.RepoDirectory );
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
            else if ( settings.Sign )
            {
                context.Console.WriteWarning( $"Cannot use --sign option in a non-public build." );

                return false;
            }

            // Writing the import file at the end of the build so it gets only written if the build was successful.
            this.WriteImportFile( context, settings.BuildConfiguration );

            context.Console.WriteSuccess( $"Building the whole {this.ProductName} product was successful. Package version: {versionInfo.PackageVersion}." );

            return true;
        }

        private void DeleteImportFile( BuildContext context, BuildConfiguration configuration )
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
            var importFileContent = $@"
<Project>
    <!-- This file must not be added to source control and must not be uploaded as a build artifact.
         It must be imported by other repos as a dependency. 
         Dependent projects should not directly reference the artifacts path, which is considered an implementation detail. -->
    <Import Project=""{context.GetManifestFilePath( configuration )}""/>
</Project>
";

            var importFilePath = Path.Combine( context.RepoDirectory, this.ProductName + ".Import.props" );

            File.WriteAllText( importFilePath, importFileContent );
        }

        private VersionInfo ReadGeneratedVersionFile( string path )
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

            return new VersionInfo( packageVersion, configuration );
        }

        private static (string MainVersion, string PackageVersionSuffix) ReadMainVersionFile( string path )
        {
            var versionFilePath = path;
            var versionFile = Project.FromFile( versionFilePath, new ProjectOptions() );

            var mainVersion = versionFile
                .Properties
                .SingleOrDefault( p => p.Name == "MainVersion" )
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

            return (mainVersion, suffix);
        }

        private Dictionary<string, string?> ReadVersionFile( string path )
        {
            var versionFile = Project.FromFile( path, new ProjectOptions() );

            var properties = this.Dependencies
                .ToDictionary(
                    d => d.Name,
                    d => versionFile.Properties.SingleOrDefault( p => p.Name == d.Name.Replace( ".", "", StringComparison.OrdinalIgnoreCase ) + "Version" )
                        ?.EvaluatedValue );

            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

            return properties;
        }

        /// <summary>
        /// An event raised when the build is completed.
        /// </summary>
        public event Func<(BuildContext Context, BuildSettings Settings, string Directory), bool>? BuildCompleted;

        /// <summary>
        /// An event raised when the Prepare phase is complete.
        /// </summary>
        public event Func<(BuildContext Context, BaseBuildSettings Settings), bool>? PrepareCompleted;

        protected virtual bool BuildCore( BuildContext context, BuildSettings settings )
        {
            foreach ( var solution in this.Solutions )
            {
                if ( settings.IncludeTests || !solution.IsTestOnly )
                {
                    context.Console.WriteHeading( $"Building {solution.Name}." );

                    if ( !settings.NoDependencies )
                    {
                        if ( !solution.Restore( context, settings ) )
                        {
                            return false;
                        }
                    }

                    switch ( solution.GetBuildMethod() )
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
                    }

                    context.Console.WriteSuccess( $"Building {solution.Name} was successful." );
                }
            }

            return true;
        }

        public bool Test( BuildContext context, BuildSettings settings )
        {
            if ( !settings.NoDependencies && !this.Build( context, (BuildSettings) settings.WithIncludeTests( true ) ) )
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
                        (BuildSettings) settings.WithAdditionalProperties( properties ).WithoutConcurrency();
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
                    new AnalyzeCoverageSettings { Path = Path.Combine( testResultsDir, "coverage.net5.0.json" ) } ) )
                {
                    return false;
                }
            }

            context.Console.WriteSuccess( $"Testing {this.ProductName} was successful" );

            return true;
        }

        public bool Prepare( BuildContext context, BaseBuildSettings settings )
        {
            if ( !settings.NoDependencies )
            {
                this.Clean( context, settings );
            }

            var (mainVersion, mainPackageVersionSuffix) =
                ReadMainVersionFile(
                    Path.Combine(
                        context.RepoDirectory,
                        this.EngineeringDirectory,
                        "MainVersion.props" ) );

            context.Console.WriteHeading( "Preparing the version file" );

            var configuration = settings.BuildConfiguration.ToString().ToLowerInvariant();

            var versionPrefix = mainVersion;
            int patchNumber;
            string versionSuffix;

            switch ( settings.VersionSpec.Kind )
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

                        versionSuffix =
                            $"local-{Environment.UserName}-{configuration}";

                        patchNumber = localVersion;

                        break;
                    }

                case VersionKind.Numbered:
                    {
                        // Build server build with a build number given by the build server
                        patchNumber = settings.VersionSpec.Number;
                        versionSuffix = $"dev-{configuration}";

                        break;
                    }

                case VersionKind.Public:
                    // Public build
                    versionSuffix = mainPackageVersionSuffix.TrimStart( '-' );
                    patchNumber = 0;

                    break;

                default:
                    throw new InvalidOperationException();
            }

            var privateArtifactsRelativeDir =
                this.PrivateArtifactsDirectory.ToString( new VersionInfo( null!, settings.BuildConfiguration.ToString() ) );

            var artifactsDir = Path.Combine( context.RepoDirectory, privateArtifactsRelativeDir );

            if ( !Directory.Exists( artifactsDir ) )
            {
                Directory.CreateDirectory( artifactsDir );
            }

            var props = this.GenerateVersionFile( versionPrefix, patchNumber, versionSuffix, configuration );
            var propsFileName = $"{this.ProductName}.version.props";
            var propsFilePath = Path.Combine( artifactsDir, propsFileName );

            context.Console.WriteMessage( $"Writing '{propsFilePath}'." );
            File.WriteAllText( propsFilePath, props );

            // Update Versions.g.props.
            var versionsOverrideFile = VersionsOverrideFile.Load( context );

            if ( versionsOverrideFile.LocalBuildFile != propsFilePath )
            {
                versionsOverrideFile.LocalBuildFile = propsFilePath;
                context.Console.WriteMessage( $"Updating '{versionsOverrideFile.FilePath}'." );
            }

            // Validate default versions. If there are not set to a public version, we need to fetch from the build server.
            // This scenario is important when building on a build server.
            Dictionary<string, string?>? defaultDependencyProperties = null;
            Dictionary<string, DependencySource> changedDependencies = new();

            foreach ( var dependency in versionsOverrideFile.Dependencies )
            {
                if ( dependency.Value.SourceKind == DependencySourceKind.Default )
                {
                    defaultDependencyProperties ??= this.ReadVersionFile( Path.Combine( context.RepoDirectory, this.VersionsFile ) );

                    if ( !defaultDependencyProperties.TryGetValue( dependency.Key, out var dependencyVersion ) || string.IsNullOrEmpty( dependencyVersion ) )
                    {
                        context.Console.WriteError( $"The default version for dependency {dependency.Key} is not set." );

                        break;
                    }
                    else if ( dependencyVersion.StartsWith( "branch:", StringComparison.OrdinalIgnoreCase ) )
                    {
                        var branch = dependencyVersion.Substring( "branch:".Length );
                        context.Console.WriteWarning( $"Fetching {dependency.Key} from build server, branch {branch}." );
                        changedDependencies[dependency.Key] = new DependencySource( DependencySourceKind.BuildServer, branch );
                    }
                }
            }

            // If we changed local dependency settings, we have to fetch.
            if ( changedDependencies.Count > 0 )
            {
                foreach ( var changedDependency in changedDependencies )
                {
                    versionsOverrideFile.Dependencies[changedDependency.Key] = changedDependency.Value;
                }

                // Fetch dependencies and reads the location of the version file.
                if ( !FetchDependencyCommand.FetchDependencies( context, versionsOverrideFile ) )
                {
                    return false;
                }

                context.Console.WriteImportantMessage( "Local dependencies changed like this:" );
                versionsOverrideFile.Print( context );
            }

            if ( !versionsOverrideFile.TrySave( context ) )
            {
                return false;
            }

            if ( this.PrepareCompleted != null )
            {
                if ( !this.PrepareCompleted.Invoke( (context, settings) ) )
                {
                    return false;
                }
            }

            context.Console.WriteSuccess(
                $"Preparing the version file was successful. {this.ProductNameWithoutDot}Version={this.ReadGeneratedVersionFile( context.GetManifestFilePath( settings.BuildConfiguration ) ).PackageVersion}" );

            return true;
        }

        protected virtual string GenerateVersionFile(
            string versionPrefix,
            int patchNumber,
            string versionSuffix,
            string configuration )
        {
            var props = $@"
<!-- This file is generated by the engineering tooling -->
<Project>
    <PropertyGroup>";

            var versionWithPath = patchNumber == 0 ? versionPrefix : versionPrefix + "." + patchNumber;

            if ( this.GenerateArcadeProperties )
            {
                // Metalama.Compiler, because of Arcade, requires the version number to be decomposed in a prefix, patch number, and suffix.
                // In Arcade, the package naming scheme is different because the patch number is not a part of the package name.

                var arcadeSuffix = "";

                if ( !string.IsNullOrEmpty( versionSuffix ) )
                {
                    arcadeSuffix += versionSuffix;
                }

                if ( patchNumber > 0 )
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

                    arcadeSuffix += patchNumber;
                }

                props += $@"
        <{this.ProductNameWithoutDot}VersionPrefix>{versionPrefix}</{this.ProductNameWithoutDot}VersionPrefix>
        <{this.ProductNameWithoutDot}VersionSuffix>{arcadeSuffix}</{this.ProductNameWithoutDot}VersionSuffix>
        <{this.ProductNameWithoutDot}VersionPatchNumber>{patchNumber}</{this.ProductNameWithoutDot}VersionPatchNumber>
        <{this.ProductNameWithoutDot}Version>{versionWithPath}</{this.ProductNameWithoutDot}Version>
        <{this.ProductNameWithoutDot}AssemblyVersion>{versionWithPath}</{this.ProductNameWithoutDot}AssemblyVersion>";
            }
            else
            {
                var packageSuffix = string.IsNullOrEmpty( versionSuffix ) ? "" : "-" + versionSuffix;

                props += $@"
        <{this.ProductNameWithoutDot}Version>{versionWithPath}{packageSuffix}</{this.ProductNameWithoutDot}Version>
        <{this.ProductNameWithoutDot}AssemblyVersion>{versionWithPath}</{this.ProductNameWithoutDot}AssemblyVersion>";
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
</Project>
";

            return props;
        }

        public void Clean( BuildContext context, BaseBuildSettings settings )
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

            var stringParameters = new VersionInfo( settings.BuildConfiguration.ToString(), null! );

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

        public bool Publish( BuildContext context, PublishSettings settings )
        {
            context.Console.WriteHeading( "Publishing files" );

            var versionFile = this.ReadGeneratedVersionFile( context.GetManifestFilePath( settings.BuildConfiguration ) );

            var stringParameters = new VersionInfo( versionFile.PackageVersion, versionFile.Configuration );

            var hasTarget = false;

            if ( !Publisher.PublishDirectory(
                context,
                settings,
                Path.Combine( context.RepoDirectory, this.PrivateArtifactsDirectory.ToString( stringParameters ) ),
                false ) )
            {
                return false;
            }

            if ( settings.Public )
            {
                if ( !Publisher.PublishDirectory(
                    context,
                    settings,
                    Path.Combine( context.RepoDirectory, this.PublicArtifactsDirectory.ToString( stringParameters ) ),
                    true ) )
                {
                    return false;
                }
            }

            if ( !hasTarget )
            {
                context.Console.WriteWarning( "No active publishing target was detected." );
            }
            else
            {
                context.Console.WriteSuccess( "Publishing has succeeded." );
            }

            return true;
        }
    }
}