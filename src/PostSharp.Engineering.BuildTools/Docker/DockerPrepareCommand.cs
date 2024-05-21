// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace PostSharp.Engineering.BuildTools.Docker;

#pragma warning disable CA1305

[UsedImplicitly]
public class DockerPrepareCommand : BaseCommand<BuildSettings>
{
    protected override bool ExecuteCore( BuildContext context, BuildSettings settings )
    {
        return TryPrepare( context, settings, out _ );
    }

    public static bool TryPrepare( BuildContext context, BuildSettings settings, [NotNullWhen( true )] out string? imageName )
    {
        var product = context.Product;

        var image = product.DockerBaseImage;

        if ( image == null )
        {
            context.Console.WriteError( "The DockerBaseImage property is not set." );

            imageName = null;

            return false;
        }

        imageName = $"{product.ProductName}-{product.ProductFamily.Version}-{image.Name}.{settings.BuildConfiguration}".ToLowerInvariant();

        context.Console.WriteHeading( $"Building docker image to build {product.ProductName}." );

        if ( !DependenciesOverrideFile.TryLoad( context, settings, settings.BuildConfiguration, out var dependencies ) )
        {
            return false;
        }

        var dockerDirectory = Path.Combine( context.RepoDirectory, product.EngineeringDirectory, "obj" );

        if ( !Directory.Exists( dockerDirectory ) )
        {
            Directory.CreateDirectory( dockerDirectory );
        }

        var dockerFilePath = Path.Combine( dockerDirectory, $"{imageName}.Dockerfile" );
        using var dockerfile = image.CreateDockfileWriter( File.CreateText( dockerFilePath ) );

        var configureCommands = new List<string>();

        dockerfile.WriteLine( $"FROM {image.Uri} AS build-env" );
        dockerfile.WritePrologue();

        // Add Docker image components.
        var imageComponents = new Dictionary<string, DockerImageComponent>();

        foreach ( var component in product.AdditionalDockerImageComponents.Concat( product.ProductFamily.DockerImageComponents ) )
        {
            imageComponents[component.Name] = component;
            component.AddPrerequisites( imageComponents );
        }

        foreach ( var component in imageComponents.OrderBy( x => x.Value.Order ) )
        {
            component.Value.AppendToDockerfile( dockerfile );
        }

        // Add source dependencies.
        foreach ( var sourceDependency in product.SourceDependencies )
        {
            AddSourceDependency( sourceDependency.Name );
        }

        // Add local dependencies.
        var localDependencies = dependencies.Dependencies
            .Where( x => x.Value.SourceKind == DependencySourceKind.Local )
            .Select( x => product.GetDependencyDefinition( x.Key ) )
            .OrderBy( x => x.BuildOrder.GetValueOrDefault( int.MaxValue ) )
            .ToList();

        foreach ( var localDependency in localDependencies )
        {
            AddLocalDependency( localDependency.Name );
        }

        var productDirectory = image.GetAbsolutePath( "src", product.ProductName );
        dockerfile.WriteLine( $"WORKDIR {dockerfile.EscapePath( productDirectory )}" );
        dockerfile.WriteLine( $"COPY . ." );
        configureCommands.Add( "ren docker-source-dependencies source-dependencies" );

        configureCommands.Add(
            $"New-Item -ItemType SymbolicLink -Path .editorconfig -Target {(image.GetRelativePath( "eng", "style", ".editorconfig" ))} | Out-Null" );

        foreach ( var localDependency in localDependencies )
        {
            configureCommands.Add( $"./Build.ps1 dependencies set local {localDependency.Name}  --nologo" );
        }

        dockerfile.RunPowerShellScript( configureCommands );

        dockerfile.Close();

        // We bypass our ToolInvocationHelper because we need ANSI output.
        var commandLine = $"build -t {imageName} --file {dockerFilePath} .";
        context.Console.WriteImportantMessage( commandLine );
        var process = Process.Start( new ProcessStartInfo( "docker", commandLine ) { UseShellExecute = false, WorkingDirectory = context.RepoDirectory } );
        process!.WaitForExit();

        return process.ExitCode == 0;

        void AddSourceDependency( string productName )
        {
            var sourceDirectory = Path.GetFullPath( Path.Combine( context.RepoDirectory, "..", productName ) );
            var targetDirectory = Path.GetFullPath( Path.Combine( context.RepoDirectory, "docker-source-dependencies", productName ) );

            var ignore = new GitIgnore( Path.Combine( sourceDirectory, ".gitignore" ) );

            FileSystemHelper.CopyFilesRecursively( context.Console, sourceDirectory, targetDirectory, f => !ignore.ShouldIgnore( f ) );
        }

        void AddLocalDependency( string productName )
        {
            var sourceDirectory = Path.GetFullPath( Path.Combine( context.RepoDirectory, "..", productName, "artifacts", "publish", "private" ) );
            var targetDirectory = Path.GetFullPath( Path.Combine( context.RepoDirectory, "dependencies", productName ) );

            configureCommands.Add( $"./Build.ps1 dependencies set {DependencySourceKind.Restored} {productName}" );

            FileSystemHelper.CopyFilesRecursively( context.Console, sourceDirectory, targetDirectory );
        }
    }
}