// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
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
        var product = context.Product;

        if ( string.IsNullOrEmpty( product.DockerBaseImage ) )
        {
            context.Console.WriteError( "The DockerBaseImage property is not set." );

            return false;
        }

        if ( !DependenciesOverrideFile.TryLoad( context, settings, settings.BuildConfiguration, out var dependencies ) )
        {
            return false;
        }

        var imageName = $"{product.ProductName}-{settings.BuildConfiguration}".ToLowerInvariant();

        var dockerDirectory = Path.Combine( context.RepoDirectory, product.EngineeringDirectory, "obj" );

        if ( !Directory.Exists( dockerDirectory ) )
        {
            Directory.CreateDirectory( dockerDirectory );
        }

        var dockerFilePath = Path.Combine( dockerDirectory, $"{imageName}.Dockerfile" );
        using var dockerfile = File.CreateText( dockerFilePath );

        var commandLine = new StringBuilder( $"buildx build -t {imageName} --file {dockerFilePath}" );

        dockerfile.WriteLine( $"FROM {product.DockerBaseImage} AS build-env" );

        if ( !BuildContext.IsGuestDevice )
        {
            dockerfile.WriteLine( "ENV IS_POSTSHARP_OWNED true" );
        }

        dockerfile.WriteLine(
            """
            RUN apt-get update && \
            apt-get install -y wget apt-transport-https software-properties-common && \
            wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
            dpkg -i packages-microsoft-prod.deb && \
            apt-get update && \
            apt-get install -y dotnet-sdk-6.0
            """ );

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
            // TODO: Depth-first order.
            AddSource( localDependency.Key, true );
            dockerfile.WriteLine( $"WORKDIR /src/{localDependency.Key}" );
            dockerfile.WriteLine( "RUN pwsh ./Build.ps1 build" ); // TODO: Only Engineering must be built upfront.
        }

        AddSource( product.ProductName, false );

        dockerfile.WriteLine( $"WORKDIR /src/{product.ProductName}" );

        foreach ( var localDependency in localDependencies )
        {
            dockerfile.WriteLine( $"RUN pwsh ./Build.ps1 dependencies set local {localDependency.Key}" );
        }

        dockerfile.WriteLine( "RUN pwsh ./Build.ps1 prepare" );
        dockerfile.WriteLine( "RUN dotnet restore" );

        dockerfile.Close();

        return ToolInvocationHelper.InvokeTool( context.Console, "docker", commandLine.ToString(), null );

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