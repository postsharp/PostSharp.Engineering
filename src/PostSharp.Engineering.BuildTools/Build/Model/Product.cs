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

        public string DependenciesDirectory { get; init; } = "dependencies";

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

        public bool Build( BuildContext context, BuildOptions options )
        {
            // Validate options.
            if ( options.PublicBuild )
            {
                if ( options.BuildConfiguration != BuildConfiguration.Release )
                {
                    context.Console.WriteError( $"Cannot build a public version of a {options.BuildConfiguration} build without --force." );

                    return false;
                }
            }

            // Build dependencies.
            if ( !options.NoDependencies && !this.Prepare( context, options ) )
            {
                return false;
            }

            // We have to read the version from the file we have generated - using MSBuild, because it contains properties.
            var versionInfo = this.ReadGeneratedVersionFile( context.GetVersionFilePath( options.BuildConfiguration ) );

            var privateArtifactsDir = Path.Combine(
                context.RepoDirectory,
                this.PrivateArtifactsDirectory.ToString( versionInfo ) );

            // Build.
            if ( !this.BuildCore( context, options ) )
            {
                return false;
            }

            // Allow for some customization before we create the zip file and copy to the public directory.
            this.BuildCompleted?.Invoke( (context, options, privateArtifactsDir) );

            // Check that the build produced the expected artifacts.
            // TODO: this will only fail if there is NO artifacts. It does not check that every item in the pattern actually matched something.
            var artifacts = new List<FilePatternMatch>();

            var allFilesPattern = this.PublicArtifacts.Add( this.PrivateArtifacts );

            if ( !allFilesPattern.TryGetFiles( privateArtifactsDir, versionInfo, artifacts ) )
            {
                context.Console.WriteError(
                    $"The build did not generate the artifacts '{allFilesPattern}' in '{privateArtifactsDir}'. $(PackageVersion)={versionInfo.PackageVersion}, $(Configuration)={versionInfo.Configuration}" );

                return false;
            }
        
            // Zipping internal artifacts.
            void CreateZip( string directory )
            {
                if ( options.CreateZip )
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
            if ( options.PublicBuild )
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

                if ( options.Sign )
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
                                $"Sign --baseDirectory {publicArtifactsDirectory} --input {filter} --config $(ToolsDirectory)\\signclient-appsettings.json --name {this.ProductName} --user sign-caravela@postsharp.net --secret %SIGNSERVER_SECRET%",
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
            else if ( options.Sign )
            {
                context.Console.WriteWarning( $"Cannot use --sign option in a non-public build." );

                return false;
            }

            context.Console.WriteSuccess( $"Building the whole {this.ProductName} product was successful. Package version: {versionInfo.PackageVersion}." );

            return true;
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
        public event Func<(BuildContext Context, BuildOptions Options, string Directory), bool>? BuildCompleted;

        protected virtual bool BuildCore( BuildContext context, BuildOptions options )
        {
            foreach ( var solution in this.Solutions )
            {
                if ( options.IncludeTests || !solution.IsTestOnly )
                {
                    context.Console.WriteHeading( $"Building {solution.Name}." );

                    if ( !options.NoDependencies )
                    {
                        if ( !solution.Restore( context, options ) )
                        {
                            return false;
                        }
                    }

                    switch ( solution.GetBuildMethod() )
                    {
                        case BuildMethod.Build:
                            if ( !solution.Build( context, options ) )
                            {
                                return false;
                            }

                            break;

                        case BuildMethod.Pack:
                            if ( solution.PackRequiresExplicitBuild && !options.NoDependencies )
                            {
                                if ( !solution.Build( context, options ) )
                                {
                                    return false;
                                }
                            }

                            if ( !solution.Pack( context, options ) )
                            {
                                return false;
                            }

                            break;

                        case BuildMethod.Test:
                            if ( !solution.Test( context, options ) )
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

        public bool Test( BuildContext context, BuildOptions options )
        {
            if ( !options.NoDependencies && !this.Build( context, (BuildOptions) options.WithIncludeTests( true ) ) )
            {
                return false;
            }

            ImmutableDictionary<string, string> properties;
            var testResultsDir = Path.Combine( context.RepoDirectory, "TestResults" );

            if ( options.AnalyzeCoverage )
            {
                // Removing the TestResults directory so that we reset the code coverage information.
                if ( Directory.Exists( testResultsDir ) )
                {
                    Directory.Delete( testResultsDir, true );
                }

                properties = options.AnalyzeCoverage
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
                var solutionOptions = options;

                if ( options.AnalyzeCoverage && solution.SupportsTestCoverage )
                {
                    solutionOptions =
                        (BuildOptions) options.WithAdditionalProperties( properties ).WithoutConcurrency();
                }

                context.Console.WriteHeading( $"Testing {solution.Name}." );

                if ( !solution.Test( context, solutionOptions ) )
                {
                    return false;
                }

                context.Console.WriteSuccess( $"Testing {solution.Name} was successful" );
            }

            if ( options.AnalyzeCoverage )
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

        public bool Prepare( BuildContext context, BaseBuildSettings options )
        {
            if ( !options.NoDependencies )
            {
                this.Clean( context, options );
            }

            var (mainVersion, mainPackageVersionSuffix) =
                ReadMainVersionFile(
                    Path.Combine(
                        context.RepoDirectory,
                        this.EngineeringDirectory,
                        "MainVersion.props" ) );

            context.Console.WriteHeading( "Preparing the version file" );

            var configuration = options.BuildConfiguration.ToString().ToLowerInvariant();

            var versionPrefix = mainVersion;
            int patchNumber;
            string versionSuffix;

            switch ( options.VersionSpec.Kind )
            {
                case VersionKind.Local:
                    {
                        // Local build with timestamp-based version and randomized package number. For the assembly version we use a local incremental file stored in the user profile.
                        var localVersionDirectory =
                            Environment.ExpandEnvironmentVariables( "%APPDATA%\\Caravela.Engineering" );

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
                        patchNumber = options.VersionSpec.Number;
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
                this.PrivateArtifactsDirectory.ToString( new VersionInfo( null!, options.BuildConfiguration.ToString() ) );

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

            // Write a link to this file in the root file of the repo. This file is the interface of the repo, which can be imported by other repos.
            var importFileContent = $@"
<Project>
    <!-- This file must not be added to source control and must not be uploaded as a build artifact.
         It must be imported by other repos as a dependency. 
         Dependent projects should not directly reference the artifacts path, which is considered an implementation detail. -->
    <Import Project=""{Path.Combine( privateArtifactsRelativeDir, propsFileName )}""/>
</Project>
";

            var importFilePath = Path.Combine( context.RepoDirectory, this.ProductName + ".Import.props" );

            File.WriteAllText(
                importFilePath,
                importFileContent );

            // Update Versions.g.props.
            var versionsOverrideFile = VersionsOverrideFile.Load( context );

            if ( versionsOverrideFile.LocalBuildFile != importFilePath )
            {
                versionsOverrideFile.LocalBuildFile = importFilePath;
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

            context.Console.WriteSuccess(
                $"Preparing the version file was successful. {this.ProductNameWithoutDot}Version={this.ReadGeneratedVersionFile( context.GetVersionFilePath( options.BuildConfiguration ) ).PackageVersion}" );

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
                // Caravela.Compiler, because of Arcade, requires the version number to be decomposed in a prefix, patch number, and suffix.
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
        <{this.ProductNameWithoutDot}EngineeringVersion>{typeof(Product).Assembly.GetName().Version}</{this.ProductNameWithoutDot}EngineeringVersion>
        <RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);$(MSBuildThisFileDirectory)</RestoreAdditionalProjectSources>
    </PropertyGroup>
</Project>
";

            return props;
        }

        public void Clean( BuildContext context, BaseBuildSettings options )
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

            var stringParameters = new VersionInfo( options.BuildConfiguration.ToString(), null! );

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

        public bool Publish( BuildContext context, PublishOptions options )
        {
            context.Console.WriteHeading( "Publishing files" );

            var versionFile = this.ReadGeneratedVersionFile( context.GetVersionFilePath( options.BuildConfiguration ) );

            var stringParameters = new VersionInfo( versionFile.PackageVersion, versionFile.Configuration );

            var hasTarget = false;

            if ( !Publisher.PublishDirectory(
                context,
                options,
                Path.Combine( context.RepoDirectory, this.PrivateArtifactsDirectory.ToString( stringParameters ) ),
                false ) )
            {
                return false;
            }

            if ( options.Public )
            {
                if ( !Publisher.PublishDirectory(
                    context,
                    options,
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