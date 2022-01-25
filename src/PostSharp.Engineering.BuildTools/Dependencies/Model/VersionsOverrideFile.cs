using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using PostSharp.Engineering.BuildTools.Build;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public class VersionsOverrideFile
    {
        private static readonly Regex _dependencyVersionRegex = new(
            "^(?<Kind>[^:]+):(?<Arguments>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant );

        private static readonly Regex _buildSettingsRegex = new(
            @"^Number=(?<Number>\d+)(;TypeId=(?<TypeId>[^;]+))?(;TypeId=(?<Branch>[^;]+))?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant );

        public Dictionary<string, DependencySource> Dependencies { get; } = new();

        public string? LocalBuildFile { get; set; }

        public string FilePath { get; }

        private VersionsOverrideFile( string path )
        {
            this.FilePath = path;
        }

        private bool TryLoadDefaultVersions( BuildContext context )
        {
            var versionsPath = Path.Combine( context.RepoDirectory, context.Product.VersionsFile );

            if ( !File.Exists( versionsPath ) )
            {
                context.Console.WriteError( $"The file '{versionsPath}' does not exist." );

                return false;
            }

            var projectOptions = new ProjectOptions { GlobalProperties = new Dictionary<string, string>() { ["VcsBranch"] = context.Branch } };
            var versionFile = Project.FromFile( versionsPath, projectOptions );

            var defaultDependencyProperties = context.Product.Dependencies
                .ToDictionary(
                    d => d.Name,
                    d => versionFile.Properties.SingleOrDefault( p => p.Name == d.NameWithoutDot + "Version" )
                        ?.EvaluatedValue );

            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

            foreach ( var dependencyDefinition in context.Product.Dependencies )
            {
                var dependencyVersion = defaultDependencyProperties[dependencyDefinition.Name];

                if ( string.IsNullOrEmpty( dependencyVersion ) )
                {
                    context.Console.WriteError( $"There is no {dependencyDefinition.NameWithoutDot}Version property in '{versionsPath}'." );

                    return false;
                }

                DependencySource dependencySource;

                if ( string.Compare( dependencyVersion, "local", StringComparison.OrdinalIgnoreCase ) == 0 )
                {
                    dependencySource = DependencySource.CreateLocal( DependencyConfigurationOrigin.Default );
                }
                else
                {
                    var dependencyVersionMatch = _dependencyVersionRegex.Match( dependencyVersion );

                    if ( dependencyVersionMatch.Success )
                    {
                        switch ( dependencyVersionMatch.Groups["Kind"].Value.ToLowerInvariant() )
                        {
                            case "branch":
                                {
                                    var branch = dependencyVersionMatch.Groups["Arguments"].Value;

                                    dependencySource = DependencySource.CreateBuildServerSource(
                                        new CiBranch( branch ),
                                        DependencyConfigurationOrigin.Default );

                                    break;
                                }

                            case "build":
                                {
                                    var arguments = dependencyVersionMatch.Groups["Arguments"].Value;

                                    var buildSettingsMatch = _buildSettingsRegex.Match( arguments );

                                    if ( !buildSettingsMatch.Success )
                                    {
                                        context.Console.WriteError(
                                            $"The TeamCity build configuration '{arguments}' of dependency '{dependencyDefinition.Name}' does not have a correct format." );

                                        return false;
                                    }

                                    var buildNumber = int.Parse( buildSettingsMatch.Groups["Number"].Value, CultureInfo.InvariantCulture );
                                    var ciBuildTypeId = buildSettingsMatch.Groups.GetValueOrDefault( "TypeId" )?.Value;

                                    if ( string.IsNullOrEmpty( ciBuildTypeId ) )
                                    {
                                        context.Console.WriteError(
                                            $"The TypeId property of dependency '{dependencyDefinition.Name}' does is required in '{versionsPath}'." );
                                    }

                                    dependencySource = DependencySource.CreateBuildServerSource(
                                        new CiBuildId( buildNumber, ciBuildTypeId ),
                                        DependencyConfigurationOrigin.Default );

                                    break;
                                }

                            case "transitive":
                                {
                                    context.Console.WriteError( $"Error in '{versionsPath}': explicit transitive dependencies are no longer supported." );

                                    return false;
                                }

                            default:
                                {
                                    context.Console.WriteError(
                                        $"Error in '{versionsPath}': cannot parse the value '{dependencyVersion}' for dependency '{dependencyDefinition.Name}' in '{versionsPath}'." );

                                    return false;
                                }
                        }
                    }
                    else if ( char.IsDigit( dependencyVersion[0] ) )
                    {
                        dependencySource = DependencySource.CreateFeed( dependencyVersion, DependencyConfigurationOrigin.Default );
                    }
                    else
                    {
                        context.Console.WriteError(
                            $"Error in '{versionsPath}': cannot parse the dependency '{dependencyDefinition.Name}' from '{versionsPath}'." );

                        return false;
                    }
                }

                this.Dependencies[dependencyDefinition.Name] = dependencySource;
            }

            return true;
        }

        public static bool TryLoad( BuildContext context, [NotNullWhen( true )] out VersionsOverrideFile? file )
        {
            var versionsOverridePath = Path.Combine(
                context.RepoDirectory,
                context.Product.EngineeringDirectory,
                "Versions.g.props" );

            file = new VersionsOverrideFile( versionsOverridePath );

            if ( !file.TryLoadDefaultVersions( context ) )
            {
                file = null;

                return false;
            }

            // Override defaults from the version file.
            if ( File.Exists( versionsOverridePath ) )
            {
                var document = XDocument.Load( versionsOverridePath );
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
                                $"The dependency kind '{kindString}' defined in '{versionsOverridePath}' is not supported. Skipping the parsing of this dependency." );

                            continue;
                        }

                        var originString = item.Element( "Origin" )?.Value;

                        if ( originString == null || !Enum.TryParse( originString, out DependencyConfigurationOrigin origin ) )
                        {
                            origin = DependencyConfigurationOrigin.Unknown;
                        }

                        switch ( kind )
                        {
                            case DependencySourceKind.Feed:
                                var version = item.Element( "Version" )?.Value;

                                // Note that the version can be null here. It means that the version should default to the version defined in Versions.props.

                                file.Dependencies[name] = DependencySource.CreateFeed( version, origin );

                                break;

                            case DependencySourceKind.Local:
                                file.Dependencies[name] = DependencySource.CreateLocal( origin );

                                break;

                            case DependencySourceKind.BuildServer:
                                var branch = item.Element( "Branch" )?.Value;
                                var buildNumber = item.Element( "BuildNumber" )?.Value;
                                var ciBuildTypeId = item.Element( "CiBuildTypeId" )?.Value;
                                var versionFile = item.Element( "VersionFile" )?.Value;

                                ICiBuildSpec buildSpec;

                                if ( !string.IsNullOrEmpty( buildNumber ) )
                                {
                                    if ( string.IsNullOrEmpty( ciBuildTypeId ) )
                                    {
                                        context.Console.WriteError(
                                            $"The property CiBuildTypeId of dependency {name} is required in '{versionsOverridePath}'." );

                                        return false;
                                    }

                                    buildSpec = new CiBuildId( int.Parse( buildNumber, CultureInfo.InvariantCulture ), ciBuildTypeId );
                                }
                                else if ( !string.IsNullOrEmpty( branch ) )
                                {
                                    buildSpec = new CiBranch( branch );
                                }
                                else
                                {
                                    context.Console.WriteError(
                                        $"The dependency {name}  in '{versionsOverridePath}' requires one of these properties: Branch or BuildNumber." );

                                    return false;
                                }

                                var dependencySource =
                                    DependencySource.CreateBuildServerSource(
                                        buildSpec,
                                        origin );

                                dependencySource.VersionFile = versionFile;
                                file.Dependencies[name] = dependencySource;

                                break;

                            default:
                                throw new InvalidVersionFileException();
                        }
                    }
                }
            }

            return true;
        }

        public bool TrySave( BuildContext context )
        {
            context.Console.WriteMessage( $"Writing '{this.FilePath}'." );
            
            var project = new XElement( "Project", new XAttribute( "InitialTargets", "VerifyProductDependencies" ) );
            var document = new XDocument( project );

            project.Add( new XComment( "This file is automatically generated. Do not edit it manually." ) );
            var requiredFiles = new List<string>();

            void AddImport( string file, bool required = true, string? label = null )
            {
                // We're generating a relative path so that the path can be resolved even when the filesystem is mounted
                // to a different location than the current one (used e.g. when using Hyper-V).
                var relativePath = "$(MSBuildThisFileDirectory)\\" + Path.GetRelativePath( Path.GetDirectoryName( this.FilePath )!, file );

                var element = new XElement( "Import", new XAttribute( "Project", relativePath ), new XAttribute( "Condition", $"Exists( '{relativePath}' )" ) );

                if ( label != null )
                {
                    element.Add( new XAttribute( "Label", label ) );
                }

                project.Add( element );

                if ( required )
                {
                    requiredFiles.Add( relativePath );
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

                var item = new XElement(
                    "LocalDependencySource",
                    new XAttribute( "Include", dependency.Key ),
                    new XElement( "Kind", dependency.Value.SourceKind ) );

                void AddIfNotNull( string name, string? value )
                {
                    if ( value != null )
                    {
                        item.Add( new XElement( name, value ) );
                    }
                }

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

                                switch ( dependency.Value.BuildServerSource )
                                {
                                    case CiBranch branch:
                                        AddIfNotNull( "Branch", branch.Name );

                                        break;

                                    case CiBuildId buildId:
                                        AddIfNotNull( "BuildNumber", buildId.BuildNumber.ToString( CultureInfo.InvariantCulture ) );
                                        AddIfNotNull( "CiBuildTypeId", buildId.BuildTypeId );

                                        break;
                                }

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

                    case DependencySourceKind.Feed:
                        {
                            AddIfNotNull( "Version", dependency.Value.Version );
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

                var rowNumber = (i + 1).ToString( CultureInfo.InvariantCulture );

                if ( !this.Dependencies.TryGetValue( name, out var source ) )
                {
                    table.AddRow( rowNumber, name, "?" );
                }
                else
                {
                    table.AddRow( rowNumber, name, source.ToString() );
                }
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