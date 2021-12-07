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

            if ( File.Exists( versionsOverridePath ) )
            {
                var project = XDocument.Load( versionsOverridePath );
                var localImport = project.Elements( "Import" ).SingleOrDefault( i => i.Attribute( "Label" )?.Value?.Equals( "Current", StringComparison.OrdinalIgnoreCase ) ?? false );
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

                        switch ( kind )
                        {
                            case DependencySourceKind.Default:
                            case DependencySourceKind.Local:
                                file.Dependencies.Add( name, new DependencySource( kind ) );

                                break;

                            case DependencySourceKind.BuildServer:
                                var branch = item.Element( "Branch" )?.Value;
                                file.Dependencies.Add( name, new DependencySource( kind, branch ) );

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

            foreach ( var dependency in this.Dependencies.OrderBy( d => d.Key ) )
            {
                var item = new XElement(
                    "LocalDependencySource",
                    new XAttribute( "Include", dependency.Key ),
                    new XElement( "Kind", dependency.Value.SourceKind ) );

                itemGroup.Add( item );

                switch ( dependency.Value.SourceKind )
                {
                    case DependencySourceKind.BuildServer:
                        {
                            var vcsInfo = context.Product.Dependencies.SingleOrDefault( p => p.Name == dependency.Key );

                            if ( vcsInfo == null )
                            {
                                context.Console.WriteError( $"The dependency '{dependency.Key}' is not added to the product." );

                                return false;
                            }

                            item.Add( new XElement( "Branch", dependency.Value.Branch ) );

                            var importProjectFile = Path.GetFullPath(
                                Path.Combine(
                                    context.RepoDirectory,
                                    "artifacts",
                                    "dependencies",
                                    dependency.Key,
                                    vcsInfo.RestoredArtifactsDirectory,
                                    dependency.Key + ".version.props" ) );

                            requiredFiles.Add( importProjectFile );
                            project.Add( new XElement( "Import", new XAttribute( "Project", importProjectFile ), CreateCondition( importProjectFile ) ) );
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

                    default:
                        throw new InvalidVersionFileException();
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

// Add some columns
            table.AddColumn( "Id" );
            table.AddColumn( "Name" );
            table.AddColumn( "Source" );

// Add some rows

            for ( var i = 0; i < context.Product.Dependencies.Length; i++ )
            {
                var name = context.Product.Dependencies[i].Name;

                if ( !this.Dependencies.TryGetValue( name, out var source ) )
                {
                    source = new DependencySource( DependencySourceKind.Default );
                }

                table.AddRow( (i + 1).ToString( CultureInfo.InvariantCulture ), name, source.ToString()! );
            }

            context.Console.Out.Write( table );
        }
    }
}