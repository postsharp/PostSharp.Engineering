// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Build.Publishers.Downloads;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace PostSharp.Engineering.BuildTools.Build.Solutions;

public class ConsolidatedBuildSolution : Solution
{
    private readonly ParametricString _zipPackageFileName;
    private readonly string _versionPackageName;

    public ConsolidatedBuildSolution( ParametricString zipPackageFileName, string versionPackageName ) : base( null! )
    {
        this._zipPackageFileName = zipPackageFileName;
        this._versionPackageName = versionPackageName;
    }

    public override bool Build( BuildContext context, BuildSettings settings ) => throw new System.NotSupportedException();

    public override bool Pack( BuildContext context, BuildSettings settings )
    {
        var dependenciesDirectory = Path.Combine( context.RepoDirectory, "dependencies" );
        
        var packageExtensions = new[] { ".nupkg", ".snupkg" };

        var packages = Directory.EnumerateFiles( dependenciesDirectory, "*.*", SearchOption.AllDirectories )
            .Where( p => packageExtensions.Contains( Path.GetExtension( p ) ) )
            .ToArray();

        var packageVersionRegex = new Regex(
            $@"^{Regex.Escape( this._versionPackageName )}\.(?<Version>\d+\.\d+\.\d+(?:-.+)?)\.nupkg$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase );

        var packageVersion = packages
            .Select( p => packageVersionRegex.Match( Path.GetFileName( p ) ) )
            .Single( m => m.Success )
            .Groups["Version"].Value;

        var buildInfo = new BuildInfo( packageVersion, settings.BuildConfiguration, context.Product, null );
        var publicArtifactsDirectory = Path.Combine( context.RepoDirectory, context.Product.PublicArtifactsDirectory.ToString( buildInfo ) );
        var zipFileName = this._zipPackageFileName.ToString( buildInfo );
        var zipFilePath = Path.Combine( publicArtifactsDirectory, zipFileName );
        
        context.Console.WriteMessage( $"Creating '{zipFilePath}' archive." );

        if ( !Directory.Exists( publicArtifactsDirectory ) )
        {
            Directory.CreateDirectory( publicArtifactsDirectory );
        }

        using ( var zipFile = ZipFile.Open( zipFilePath, ZipArchiveMode.Create ) )
        {
            foreach ( var package in packages )
            {
                context.Console.WriteMessage( $"Adding '{package}' package." );
                zipFile.CreateEntryFromFile( package, Path.GetFileName( package ) );
            }
        }
        
        context.Console.WriteMessage( "Creating index files." );
        
        var downloadsFolder = DownloadsFolder.Create( context, buildInfo );

        var packageDownloadFile = DownloadsFile.Create(
            zipFilePath,
            "All NuGet packages in a zip file.",
            null );
    
        var mainIndex = new DownloadsIndex( downloadsFolder, null, true );
        DownloadsIndexGenerator.Generate( mainIndex, publicArtifactsDirectory );

        var packageIndex = new DownloadsIndex( downloadsFolder.WithFiles( new[] { packageDownloadFile } ), zipFileName, false );
        DownloadsIndexGenerator.Generate( packageIndex, publicArtifactsDirectory );

        return true;
    }

    public override bool Test( BuildContext context, BuildSettings settings ) => throw new System.NotSupportedException();

    public override bool Restore( BuildContext context, BuildSettings settings ) => true;
}