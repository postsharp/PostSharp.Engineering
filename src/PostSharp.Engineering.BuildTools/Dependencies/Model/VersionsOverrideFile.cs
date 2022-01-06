﻿using PostSharp.Engineering.BuildTools.Build;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class VersionsOverrideFile
    {
        public Dictionary<string, DependencySource> Dependencies { get; } = new();

        public string? LocalBuildFile { get; set; }

        public string FilePath { get; }

        public VersionsOverrideFile( string path )
        {
            this.FilePath = path;
        }

        public static VersionsOverrideFile Load( BuildContext context )
        {
            var versionsOverridePath = Path.Combine(
                context.RepoDirectory,
                context.Product.EngineeringDirectory,
                "Versions.g.props" );

            VersionsOverrideFile file = new( versionsOverridePath );

            // By default, all dependencies source from the public feeds.
            foreach ( var dependency in context.Product.Dependencies )
            {
                file.Dependencies[dependency.Name] = DependencySource.CreateOfKind( DependencySourceKind.Default, "default" );
            }

            // Override defaults from the version file.
            if ( File.Exists( versionsOverridePath ) )
            {
                var document = XDocument.Load( versionsOverridePath );
                var project = document.Root!;

                var localImport = project.Elements( "Import" )
                    .SingleOrDefault( i => i.Attribute( "Label" )?.Value?.Equals( "Current", StringComparison.OrdinalIgnoreCase ) ?? false );

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

                        var kind = Enum.Parse<DependencySourceKind>( kindString );

                        var origin = item.Element( "Origin" )?.Value ?? "version file";

                        switch ( kind )
                        {
                            case DependencySourceKind.Default:
                            case DependencySourceKind.Local:
                                file.Dependencies[name] = DependencySource.CreateOfKind( kind, origin );

                                break;

                            case DependencySourceKind.Transitive:
                                var versionDefiningDependencyName = item.Element( "VersionDefiningDependencyName" )?.Value;
                                var defaultVersion = item.Element( "DefaultVersion" )?.Value;

                                if ( versionDefiningDependencyName == null || defaultVersion == null )
                                {
                                    throw new InvalidVersionFileException();
                                }

                                file.Dependencies[name] = DependencySource.CreateTransitiveBuildServerSource(
                                    versionDefiningDependencyName,
                                    defaultVersion,
                                    origin );

                                break;

                            case DependencySourceKind.BuildServer:
                                var branch = item.Element( "Branch" )?.Value;
                                var buildNumber = item.Element( "BuildNumber" )?.Value;
                                var ciBuildTypeId = item.Element( "CiBuildTypeId" )?.Value;
                                var versionFile = item.Element( "VersionFile" )?.Value;

                                DependencySource dependencySource;

                                if ( buildNumber != null )
                                {
                                    dependencySource = DependencySource.CreateBuildServerSource(
                                        int.Parse( buildNumber, CultureInfo.InvariantCulture ),
                                        ciBuildTypeId,
                                        branch,
                                        origin );
                                }
                                else if ( branch != null )
                                {
                                    dependencySource = DependencySource.CreateBuildServerSource( branch, ciBuildTypeId, origin );
                                }
                                else
                                {
                                    throw new InvalidVersionFileException();
                                }

                                dependencySource.VersionFile = versionFile;
                                file.Dependencies[name] = dependencySource;

                                break;

                            default:
                                throw new InvalidVersionFileException();
                        }
                    }
                }
            }

            return file;
        }

        public bool TrySave( BuildContext context )
        {
            var project = new XElement( "Project", new XAttribute( "InitialTargets", "VerifyProductDependencies" ) );
            var document = new XDocument( project );

            project.Add( new XComment( "This file is automatically generated. Do not edit it manually." ) );

            static XAttribute CreateCondition( string file )
            {
                return new XAttribute( "Condition", $"Exists( '{file}' )" );
            }

            if ( this.LocalBuildFile != null )
            {
                project.Add(
                    new XElement(
                        "Import",
                        new XAttribute( "Project", this.LocalBuildFile ),
                        CreateCondition( this.LocalBuildFile ),
                        new XAttribute( "Label", "Current" ) ) );
            }

            var itemGroup = new XElement( "ItemGroup" );
            project.Add( itemGroup );
            var requiredFiles = new List<string>();
            var transitiveVersions = new List<(string PropertyName, string Version)>();

            foreach ( var dependency in this.Dependencies.OrderBy( d => d.Key ) )
            {
                var ignoreDependency = false;

                var item = new XElement(
                    "LocalDependencySource",
                    new XAttribute( "Include", dependency.Key ),
                    new XElement( "Kind", dependency.Value.SourceKind ) );

                switch ( dependency.Value.SourceKind )
                {
                    case DependencySourceKind.BuildServer:
                        {
                            var dependencyDefinition = context.Product.Dependencies.SingleOrDefault( p => p.Name == dependency.Key )
                                                       ?? Model.Dependencies.All.SingleOrDefault( d => d.Name == dependency.Key );

                            if ( dependencyDefinition == null )
                            {
                                context.Console.WriteWarning( $"The dependency '{dependency.Key}' is not configured. Ignoring." );
                                ignoreDependency = true;
                            }
                            else
                            {
                                var versionFile = dependency.Value.VersionFile;

                                if ( versionFile == null )
                                {
                                    throw new InvalidOperationException( "The VersionFile property of dependencies should be set." );
                                }

                                void AddIfNotNull( string name, string? value )
                                {
                                    if ( value != null )
                                    {
                                        item!.Add( new XElement( name, value ) );
                                    }
                                }

                                AddIfNotNull( "Branch", dependency.Value.Branch );
                                AddIfNotNull( "BuildNumber", dependency.Value.BuildNumber?.ToString( CultureInfo.InvariantCulture ) );
                                AddIfNotNull( "CiBuildTypeId", dependency.Value.CiBuildTypeId );
                                AddIfNotNull( "VersionFile", versionFile );

                                requiredFiles.Add( versionFile );
                                project.Add( new XElement( "Import", new XAttribute( "Project", versionFile ), CreateCondition( versionFile ) ) );
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

                            project.Add( new XElement( "Import", new XAttribute( "Project", importProjectFile ), CreateCondition( importProjectFile ) ) );
                            requiredFiles.Add( importProjectFile );
                        }

                        break;

                    case DependencySourceKind.Default:
                        break;

                    case DependencySourceKind.Transitive:
                        {
                            item.Add( new XElement( "VersionDefiningDependencyName", dependency.Value.VersionDefiningDependencyName ) );
                            item.Add( new XElement( "DefaultVersion", dependency.Value.DefaultVersion ) );

                            var versionPropertyName = dependency.Key.Replace( ".", "", StringComparison.OrdinalIgnoreCase ) + "Version";
                            transitiveVersions.Add( (versionPropertyName, dependency.Value.DefaultVersion!) );
                        }

                        break;

                    default:
                        throw new InvalidVersionFileException();
                }

                item.Add( new XElement( "Origin", dependency.Value.Origin ) );

                if ( !ignoreDependency )
                {
                    itemGroup.Add( item );
                }
            }

            if ( transitiveVersions.Count > 0 )
            {
                var transitiveVersionsPropertyGroup = new XElement( "PropertyGroup" );
                project.Add( transitiveVersionsPropertyGroup );

                foreach ( var transitiveVersion in transitiveVersions )
                {
                    transitiveVersionsPropertyGroup.Add( new XElement( transitiveVersion.PropertyName, transitiveVersion.Version ) );
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

            // Add direct dependencies.
            for ( var i = 0; i < context.Product.Dependencies.Length; i++ )
            {
                var name = context.Product.Dependencies[i].Name;

                if ( !this.Dependencies.TryGetValue( name, out var source ) )
                {
                    source = DependencySource.CreateOfKind( DependencySourceKind.Default, "print" );
                }

                table.AddRow( (i + 1).ToString( CultureInfo.InvariantCulture ), name, source.ToString()! );
            }

            // Add implicit dependencies (if previously fetched).
            foreach ( var dependency in this.Dependencies )
            {
                if ( context.Product.Dependencies.Any( d => d.Name == dependency.Key ) )
                {
                    continue;
                }

                table.AddRow( "*", dependency.Key, dependency.Value.ToString() );
            }

            context.Console.Out.Write( table );
        }
    }
}