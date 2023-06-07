﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    /// <summary>
    /// Represents the <c>MyProduct.Configuration.g.props</c> file that contains the dependencies as configured by the <c>dependencies set</c> command.
    /// </summary>
    public class DependenciesOverrideFile
    {
        public BuildConfiguration Configuration { get; }

        public Dictionary<string, DependencySource> Dependencies { get; } = new();

        public string? LocalBuildFile { get; set; }

        public string FilePath { get; }

        private DependenciesOverrideFile( string path, BuildConfiguration configuration )
        {
            this.FilePath = path;
            this.Configuration = configuration;
        }

        /// <summary>
        /// Loads the versions defined in Versions.props based on the Product definition.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private bool TryLoadDefaultDependencies( BuildContext context, CommonCommandSettings settings )
        {
            if ( !VersionFile.TryRead( context, settings, out var versionFile ) )
            {
                return false;
            }

            foreach ( var dependency in versionFile.Dependencies )
            {
                this.Dependencies[dependency.Key] = dependency.Value;
            }

            return true;
        }

        public static bool TryLoadDefaultsOnly(
            BuildContext context,
            CommonCommandSettings settings,
            BuildConfiguration configuration,
            [NotNullWhen( true )] out DependenciesOverrideFile? file )
        {
            var configurationSpecificVersionFilePath = Path.Combine(
                context.RepoDirectory,
                context.Product.EngineeringDirectory,
                $"Versions.{configuration}.g.props" );

            file = new DependenciesOverrideFile( configurationSpecificVersionFilePath, configuration );

            if ( !file.TryLoadDefaultDependencies( context, settings ) )
            {
                file = null;

                return false;
            }

            return true;
        }

        public static bool TryLoad( BuildContext context, CommonCommandSettings settings, BuildConfiguration configuration, [NotNullWhen( true )] out DependenciesOverrideFile? file )
        {
            if ( !TryLoadDefaultsOnly( context, settings, configuration, out file ) )
            {
                return false;
            }

            var filePath = file.FilePath;

            if ( !File.Exists( filePath ) )
            {
                return true;
            }

            // Override defaults from the version file.
            var document = XDocument.Load( filePath );
            var project = document.Root!;

            var localImport = project.Elements( "Import" )
                .SingleOrDefault( i => i.Attribute( "Label" )?.Value.Equals( "Current", StringComparison.OrdinalIgnoreCase ) ?? false );

            file.LocalBuildFile = localImport?.Attribute( "Project" )?.Value;

            var itemGroup = project.Element( "ItemGroup" );

            if ( itemGroup != null )
            {
                foreach ( var item in itemGroup.Elements() )
                {
                    var name = item.Attribute( "Include" )?.Value;
                    var kindString = item.Element( "Kind" )?.Value;

                    if ( name == null || kindString == null )
                    {
                        context.Console.WriteMessage( $"Invalid dependency file." );

                        continue;
                    }

                    if ( !Enum.TryParse<DependencySourceKind>( kindString, out var kind ) )
                    {
                        context.Console.WriteWarning(
                            $"The dependency kind '{kindString}' defined in '{filePath}' is not supported. Skipping the parsing of this dependency." );

                        continue;
                    }

                    var originString = item.Element( "Origin" )?.Value;

                    if ( originString == null || !Enum.TryParse( originString, out DependencyConfigurationOrigin origin ) )
                    {
                        origin = DependencyConfigurationOrigin.Unknown;
                    }

                    bool TryGetBuildId( out string? versionFile1, [NotNullWhen( true )] out ICiBuildSpec? ciBuildSpec )
                    {
                        var branch = item.Element( "Branch" )?.Value;
                        var buildNumber = item.Element( "BuildNumber" )?.Value;
                        var ciBuildTypeId = item.Element( "CiBuildTypeId" )?.Value;
                        versionFile1 = item.Element( "VersionFile" )?.Value;

                        if ( !string.IsNullOrEmpty( buildNumber ) )
                        {
                            if ( string.IsNullOrEmpty( ciBuildTypeId ) )
                            {
                                context.Console.WriteError( $"The property CiBuildTypeId of dependency {name} is required in '{filePath}'." );

                                ciBuildSpec = null;

                                return false;
                            }

                            ciBuildSpec = new CiBuildId( int.Parse( buildNumber, CultureInfo.InvariantCulture ), ciBuildTypeId );
                        }
                        else if ( !string.IsNullOrEmpty( branch ) )
                        {
                            ciBuildSpec = new CiLatestBuildOfBranch( branch );
                        }
                        else
                        {
                            context.Console.WriteError( $"The dependency {name}  in '{filePath}' requires one of these properties: Branch or BuildNumber." );

                            ciBuildSpec = null;

                            return false;
                        }

                        return true;
                    }

                    switch ( kind )
                    {
                        case DependencySourceKind.Feed:
                            var version = item.Element( "Version" )?.Value;

                            // Note that the version can be null here. It means that the version should default to the version defined in Versions.props.

                            file.Dependencies[name] = DependencySource.CreateFeed( version, origin );

                            break;

                        case DependencySourceKind.Local:
                            {
                                var dependencySource = DependencySource.CreateLocalRepo( origin );

                                dependencySource.VersionFile = Path.GetFullPath(
                                    Path.Combine(
                                        context.RepoDirectory,
                                        "..",
                                        name,
                                        name + ".Import.props" ) );

                                file.Dependencies[name] = dependencySource;

                                break;
                            }

                        case DependencySourceKind.RestoredDependency:
                            {
                                if ( !TryGetBuildId( out var versionFile, out var buildSpec ) )
                                {
                                    return false;
                                }

                                if ( TeamCityHelper.IsTeamCityBuild( settings ) )
                                {
                                    var dependencySource = DependencySource.CreateRestoredDependency( (CiBuildId) buildSpec, origin );

                                    dependencySource.VersionFile = Path.GetFullPath(
                                        Path.Combine(
                                            context.RepoDirectory,
                                            "dependencies",
                                            name,
                                            name + ".version.props" ) );

                                    file.Dependencies[name] = dependencySource;
                                }
                                else
                                {
                                    // We can have a LocalDependency on a developer machine because of transitive dependencies
                                    // of TeamCity build. In this case, we consider that the source is the CI build itself
                                    // -- the exact build number with which the first-level dependency was built.

                                    var dependencySource =
                                        DependencySource.CreateBuildServerSource(
                                            buildSpec,
                                            origin );

                                    dependencySource.VersionFile = versionFile;
                                    file.Dependencies[name] = dependencySource;
                                }

                                break;
                            }

                        case DependencySourceKind.BuildServer:
                            {
                                if ( !TryGetBuildId( out var versionFile, out var buildSpec ) )
                                {
                                    return false;
                                }

                                var dependencySource =
                                    DependencySource.CreateBuildServerSource(
                                        buildSpec,
                                        origin );

                                dependencySource.VersionFile = versionFile;
                                file.Dependencies[name] = dependencySource;

                                break;
                            }

                        default:
                            throw new InvalidVersionFileException();
                    }
                }
            }

            return true;
        }

        public bool TrySave( BuildContext context, CommonCommandSettings settings )
        {
            context.Console.WriteMessage( $"Writing '{this.FilePath}'." );

            var project = new XElement( "Project", new XAttribute( "InitialTargets", "VerifyProductDependencies" ) );
            var document = new XDocument( project );

            project.Add( new XComment( $"File generated by PostSharp.Engineering {VersionHelper.EngineeringVersion}." ) );

            var properties = new XElement( "PropertyGroup" );
            project.Add( properties );
            properties.Add( new XElement( "PostSharpEngineeringExePath", context.Product.BuildExePath ) );

            var requiredFiles = new List<string>();

            void AddImport( string file, bool required = true, string? label = null )
            {
                // We used to generate relative paths and not absolute because the filesystem could be accessed from a different machine or virtual
                // machine. Now, we are using absolute path because we want to support junctions in source dependencies. It seems that both
                // requirements cannot be reconciled.

                var element = new XElement( "Import", new XAttribute( "Project", file ), new XAttribute( "Condition", $"Exists( '{file}' )" ) );

                if ( label != null )
                {
                    element.Add( new XAttribute( "Label", label ) );
                }

                project.Add( element );

                if ( required )
                {
                    requiredFiles.Add( file );
                }
            }

            if ( this.LocalBuildFile != null )
            {
                AddImport( this.LocalBuildFile, false, "Current" );
            }

            var itemGroup = new XElement( "ItemGroup" );
            project.Add( itemGroup );

            foreach ( var dependency in this.Dependencies.OrderBy( d => d.Key ) )
            {
                var ignoreDependency = false;

                var dependencySource = dependency.Value;

                var item = new XElement(
                    "LocalDependencySource",
                    new XAttribute( "Include", dependency.Key ),
                    new XElement( "Kind", dependencySource.SourceKind ) );

                void AddIfNotNull( string name, string? value )
                {
                    if ( value != null )
                    {
                        item.Add( new XElement( name, value ) );
                    }
                }

                void WriteBuildServerSource()
                {
                    switch ( dependencySource.BuildServerSource )
                    {
                        case CiLatestBuildOfBranch branch:
                            AddIfNotNull( "Branch", branch.Name );

                            break;

                        case CiBuildId buildId:
                            AddIfNotNull( "BuildNumber", buildId.BuildNumber.ToString( CultureInfo.InvariantCulture ) );
                            AddIfNotNull( "CiBuildTypeId", buildId.BuildTypeId );

                            break;
                    }
                }

                switch ( dependencySource.SourceKind )
                {
                    case DependencySourceKind.BuildServer:
                    case DependencySourceKind.RestoredDependency when !TeamCityHelper.IsTeamCityBuild( settings ):
                        {
                            var dependencyDefinition = context.Product.Dependencies.SingleOrDefault( p => p.Name == dependency.Key ) ??
                                                       context.Product.ProductFamily.GetDependencyDefinitionOrNull( dependency.Key );

                            if ( dependencyDefinition == null )
                            {
                                context.Console.WriteWarning( $"The dependency '{dependency.Key}' is not configured. Ignoring." );
                                ignoreDependency = true;
                            }
                            else
                            {
                                var versionFile = dependencySource.VersionFile;

                                if ( versionFile == null )
                                {
                                    throw new InvalidOperationException( "The VersionFile property of dependencies should be set." );
                                }

                                WriteBuildServerSource();

                                AddIfNotNull( "VersionFile", versionFile );
                                AddImport( versionFile );
                            }
                        }

                        break;

                    case DependencySourceKind.Local:
                        {
                            var importProjectFile = Path.GetFullPath(
                                Path.Combine(
                                    context.RepoDirectory,
                                    "..",
                                    dependency.Key,
                                    dependency.Key + ".Import.props" ) );

                            AddImport( importProjectFile );
                        }

                        break;

                    case DependencySourceKind.RestoredDependency:
                        {
                            var importProjectFile = Path.GetFullPath(
                                Path.Combine(
                                    context.RepoDirectory,
                                    "dependencies",
                                    dependency.Key,
                                    dependency.Key + ".version.props" ) );

                            AddImport( importProjectFile );

                            WriteBuildServerSource();
                        }

                        break;

                    case DependencySourceKind.Feed:
                        {
                            AddIfNotNull( "Version", dependencySource.Version );
                        }

                        break;

                    default:
                        throw new InvalidVersionFileException();
                }

                item.Add( new XElement( "Origin", dependencySource.Origin ) );

                if ( !ignoreDependency )
                {
                    itemGroup.Add( item );
                }
            }

            var verifyFilesTarget = new XElement(
                "Target",
                new XAttribute( "Name", "VerifyProductDependencies" ),
                new XAttribute( "Condition", "!$(MSBuildProjectName.StartsWith('Build'))" ) );

            project.Add( verifyFilesTarget );

            foreach ( var requiredFile in requiredFiles )
            {
                verifyFilesTarget.Add(
                    new XElement(
                        "Error",
                        new XAttribute( "Text", $"The dependency '{requiredFile}' is missing." ),
                        new XAttribute( "Condition", $"!Exists( '{requiredFile}' )" ) ) );
            }

            document.Save( this.FilePath );

            return true;
        }

        public void Print( BuildContext context )
        {
            var table = new Table();

            table.AddColumn( "Id" );
            table.AddColumn( "Name" );
            table.AddColumn( "Source" );
            table.AddColumn( "Path" );

            // Add direct dependencies.
            for ( var i = 0; i < context.Product.Dependencies.Length; i++ )
            {
                var name = context.Product.Dependencies[i].Name;

                var rowNumber = (i + 1).ToString( CultureInfo.InvariantCulture );

                if ( !this.Dependencies.TryGetValue( name, out var source ) )
                {
                    table.AddRow( rowNumber, name, "<missing>", "" );
                }
                else
                {
                    table.AddRow( rowNumber, name, source.ToString(), source.VersionFile ?? "" );
                }
            }

            // Add implicit dependencies (if previously fetched).
            foreach ( var dependency in this.Dependencies )
            {
                if ( context.Product.Dependencies.Any( d => d.Name == dependency.Key ) )
                {
                    continue;
                }

                table.AddRow( "*", dependency.Key, dependency.Value.ToString(), dependency.Value.VersionFile ?? "" );
            }

            context.Console.Out.Write( table );
        }

        public void Fetch( BuildContext context )
        {
            // If we have any non-feed dependency that does not have a resolved VersionFile, it means that we have not fetched yet. 
            if ( this.Dependencies.Any( d => d.Value.SourceKind != DependencySourceKind.Feed && d.Value.VersionFile == null ) )
            {
                context.Console.WriteMessage( $"Fetching dependencies for configuration {this.Configuration}." );
                BaseFetchDependencyCommand.UpdateOrFetchDependencies( context, this.Configuration, this, false );
            }
        }
    }
}