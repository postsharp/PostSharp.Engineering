// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Docker;

#pragma warning disable CA1305

[UsedImplicitly]
public class DockerPrepareCommand : BaseCommand<DockerSettings>
{
    protected override bool ExecuteCore( BuildContext context, DockerSettings settings )
    {
        return TryPrepare( context, settings, out _, out _ );
    }

    public static bool TryPrepare(
        BuildContext context,
        DockerSettings settings,
        [NotNullWhen( true )] out string? imageName,
        [NotNullWhen( true )] out DockerImage? baseImage )
    {
        var product = context.Product;

        if ( settings.ImageName != null )
        {
            baseImage = DockerImages.All.SingleOrDefault( x => x.Name.Equals( settings.ImageName, StringComparison.OrdinalIgnoreCase ) );

            if ( baseImage == null )
            {
                context.Console.WriteError( $"There is no docker base image named '{settings.ImageName}'. Use the `docker list-images` command." );

                imageName = null;

                return false;
            }
        }
        else
        {
            baseImage = product.DockerBaseImage;

            if ( baseImage == null )
            {
                context.Console.WriteError( "The DockerBaseImage property is not set." );

                imageName = null;

                return false;
            }
        }

        imageName = $"{product.ProductName}-{product.ProductFamily.Version}-{baseImage.Name}-{settings.BuildConfiguration}".ToLowerInvariant();

        context.Console.WriteHeading( $"Building docker image to build {product.ProductName}." );

        if ( !DependenciesOverrideFile.TryLoad( context, settings, settings.BuildConfiguration, out var dependencies ) )
        {
            return false;
        }

        var dependenciesDirectory = Path.Combine( context.RepoDirectory, "dependencies" );
        var dockerDirectory = Path.Combine( context.RepoDirectory, product.EngineeringDirectory, "obj" );
        var productDirectory = baseImage.GetAbsolutePath( "src", product.ProductName );

        if ( !Directory.Exists( dockerDirectory ) )
        {
            Directory.CreateDirectory( dockerDirectory );
        }

        var dockerFilePath = Path.Combine( dockerDirectory, $"{imageName}.Dockerfile" );
        using var dockerfile = baseImage.CreateDockfileWriter( File.CreateText( dockerFilePath ) );

        var configureCommands = new List<string>();
        configureCommands.Add( "echo Running ./dependencies/ConfigureContainer.ps1" );
        configureCommands.Add( $"cd {productDirectory}" );

        dockerfile.WriteLine( $"FROM {baseImage.Uri} AS build-env" );
        dockerfile.WritePrologue( settings );

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

        dockerfile.WriteLine( $"WORKDIR {dockerfile.EscapePath( productDirectory )}" );
        dockerfile.WriteLine( $"COPY . ." );
        configureCommands.Add( "mv docker-source-dependencies source-dependencies" );

        configureCommands.Add(
            $"New-Item -ItemType SymbolicLink -Path .editorconfig -Target {baseImage.GetRelativePath( "eng", "style", ".editorconfig" )} | Out-Null" );

        // We don't run the Configure commands in the build container step (but during container execution)
        // because we don't benefit from caches during the build step.
        Directory.CreateDirectory( dependenciesDirectory );
        File.WriteAllLines( Path.Combine( dependenciesDirectory, "ConfigureContainer.ps1" ), configureCommands );

        dockerfile.Close();

        // We bypass our ToolInvocationHelper because we need ANSI output.
        var commandLine = $"build -t {imageName} --file {dockerFilePath} .";
        context.Console.WriteImportantMessage( commandLine );
        var process = Process.Start( new ProcessStartInfo( "docker", commandLine ) { UseShellExecute = false, WorkingDirectory = context.RepoDirectory } );
        process!.WaitForExit();

        return process.ExitCode == 0;

        void AddSourceDependency( string productName )
        {
            var sourceDirectory = Path.GetFullPath( Path.Combine( context.RepoDirectory, "source-dependencies", productName ) );
            var targetDirectory = Path.GetFullPath( Path.Combine( context.RepoDirectory, "docker-source-dependencies", productName ) );

            var ignore = new DockerIgnore( Path.Combine( sourceDirectory, ".dockerignore" ) );

            FileSystemHelper.CopyFilesRecursively( context.Console, sourceDirectory, targetDirectory, f => !ignore.ShouldIgnore( f ) );
        }

        void AddLocalDependency( string productName )
        {
            var sourceDirectory = Path.GetFullPath( Path.Combine( context.RepoDirectory, "..", productName, "artifacts", "publish", "private" ) );
            var targetDirectory = Path.GetFullPath( Path.Combine( context.RepoDirectory, "dependencies", productName ) );

            configureCommands.Add( $"./Build.ps1 dependencies set {DependencySourceKind.RestoredDependency} {productName}" );

            FileSystemHelper.CopyFilesRecursively( context.Console, sourceDirectory, targetDirectory );
        }
    }
}