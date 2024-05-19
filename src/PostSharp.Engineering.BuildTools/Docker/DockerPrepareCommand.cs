﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
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
        imageName = $"{product.ProductName}-{settings.BuildConfiguration}".ToLowerInvariant();

        if ( string.IsNullOrEmpty( product.DockerBaseImage ) )
        {
            context.Console.WriteError( "The DockerBaseImage property is not set." );

            return false;
        }

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
        using var dockerfile = File.CreateText( dockerFilePath );

        var commandLine = new StringBuilder( $"buildx build -t {imageName} --file {dockerFilePath}" );
        var configure = new StringBuilder();

        dockerfile.WriteLine( $"FROM {product.DockerBaseImage} AS build-env" );

        // Configure package caches.
        dockerfile.WriteLine( "ENV NUGET_PACKAGES=/root/.nuget/packages" );
        dockerfile.WriteLine( "VOLUME /root/.nuget/packages" );
        dockerfile.WriteLine( "VOLUME /tmp/.build-artifacts" );

        // Configuring bind mounts.
        dockerfile.WriteLine( "VOLUME /root/.local/PostSharp.Engineering" );
        dockerfile.WriteLine( $"VOLUME /src/{product.ProductName}/artifacts" );

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
            AddSource( sourceDependency.Name, true );
        }

        // Add local dependencies.
        var localDependencies = dependencies.Dependencies
            .Where( x => x.Value.SourceKind == DependencySourceKind.Local )
            .ToList();

        foreach ( var localDependency in localDependencies )
        {
            // TODO: Depth-first order. However this is not possible because we only have the knowledge of direct dependencies.
            AddSource( localDependency.Key, true );
            configure.AppendLine( $"cd /src/{localDependency.Key}" );
            configure.AppendLine( $"./Build.ps1 build --nologo" ); // TODO: Only Engineering must be built upfront.
        }

        AddSource( product.ProductName, false );

        dockerfile.WriteLine( $"WORKDIR /src/{product.ProductName}" );

        configure.AppendLine( $"cd /src/{product.ProductName}" );

        foreach ( var localDependency in localDependencies )
        {
            configure.AppendLine( $"./Build.ps1 dependencies set local {localDependency.Key}  --nologo" );
        }

        dockerfile.WriteLine( "COPY <<EOF Configure.ps1" );
        dockerfile.WriteLine( configure.ToString() );
        dockerfile.WriteLine( "EOF" );

        dockerfile.Close();

        // We bypass our ToolInvocationHelper because we need ANSI output.
        var process = Process.Start( new ProcessStartInfo( "docker", commandLine.ToString() ) { UseShellExecute = false } );
        process!.WaitForExit();

        return process.ExitCode == 0;

        void AddSource( string productName, bool isDependency )
        {
            var sourceDirectory = Path.GetFullPath( Path.Combine( context.RepoDirectory, "..", productName ) );

            dockerfile.WriteLine( $"RUN mkdir /src/{productName} -p" );

            if ( isDependency )
            {
                dockerfile.WriteLine( $"COPY --from={productName.ToLowerInvariant()} . /src/{productName}" );
                commandLine.Append( $" --build-context {productName.ToLowerInvariant()}={sourceDirectory}" );
            }
            else
            {
                dockerfile.WriteLine( $"COPY . /src/{productName}" );
                commandLine.Append( $" {sourceDirectory}" );
            }

            dockerfile.WriteLine(
                $"""
                 RUN rm /src/{productName}/.editorconfig && \
                 ln -s /src/{productName}/eng/style/.editorconfig  /src/{productName}/.editorconfig
                 """ );
        }
    }
}