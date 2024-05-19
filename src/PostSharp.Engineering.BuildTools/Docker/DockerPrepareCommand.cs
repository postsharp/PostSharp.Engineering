// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

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

        if ( product.DockerBaseImage == null )
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
        using var dockerfile = product.DockerBaseImage.CreateDockfileWriter( File.CreateText( dockerFilePath ) );

        var commandLine = new StringBuilder( $"buildx build -t {imageName} --file {dockerFilePath}" );
        var configure = new StringBuilder();

        dockerfile.WriteLine( $"FROM {product.DockerBaseImage.Name} AS build-env" );

        dockerfile.WritePrologue();
        dockerfile.WriteLine( "ENV NUGET_PACKAGES=/root/.nuget/packages" );

        // Configuring bind mounts.
        var artifactsDirectory = dockerfile.GetPath( "src", product.ProductName, "artifacts" );
        dockerfile.WriteLine( $"VOLUME {artifactsDirectory}" );

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
            .Select( x => product.GetDependencyDefinition( x.Key ) )
            .OrderBy( x => x.BuildOrder.GetValueOrDefault( int.MaxValue ) )
            .ToList();

        foreach ( var localDependency in localDependencies )
        {
            var dependencyDirectory = dockerfile.GetPath( "src", localDependency.Name );
            AddSource( localDependency.Name, true );
            configure.AppendLine( $"cd {dependencyDirectory}" );
            configure.AppendLine( $"./Build.ps1 build {settings}" );
        }

        AddSource( product.ProductName, false );

        var productDirectory = dockerfile.GetPath( "src", product.ProductName );
        dockerfile.WriteLine( $"WORKDIR {productDirectory}" );

        configure.AppendLine( $"cd {productDirectory}" );

        foreach ( var localDependency in localDependencies )
        {
            configure.AppendLine( $"./Build.ps1 dependencies set local {localDependency.Name}  --nologo" );
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
            var hostSourceDirectory = Path.GetFullPath( Path.Combine( context.RepoDirectory, "..", productName ) );
            var containerSourceDirectory = dockerfile.GetPath( "src", productName );

            if ( isDependency )
            {
                dockerfile.WriteLine( $"COPY --from={productName.ToLowerInvariant()} . {containerSourceDirectory}" );
                commandLine.Append( $" --build-context {productName.ToLowerInvariant()}={hostSourceDirectory}" );
            }
            else
            {
                dockerfile.WriteLine( $"COPY . {containerSourceDirectory}" );
                commandLine.Append( $" {hostSourceDirectory}" );
            }

            // the following should not be required on Windows.
            if ( product.DockerBaseImage is not DockerWindowsImage )
            {
                dockerfile.ReplaceLink(
                    dockerfile.GetPath( "src", productName, "eng", "style", ".editorconfig" ),
                    dockerfile.GetPath( "src", productName, ".editorconfig" ) );
            }
        }
    }
}