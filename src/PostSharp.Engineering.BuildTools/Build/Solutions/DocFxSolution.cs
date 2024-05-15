using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace PostSharp.Engineering.BuildTools.Build.Solutions;

[PublicAPI]
public class DocFxSolution : Solution
{
    private readonly string _archiveName;

    public DocFxSolution( string solutionPath, string archiveName ) : base( solutionPath )
    {
        // Packing is done by the publish command.
        this.BuildMethod = PostSharp.Engineering.BuildTools.Build.Model.BuildMethod.Pack;
        this._archiveName = archiveName;
    }

    public override bool Build( BuildContext context, BuildSettings settings )
    {
        var options = new ToolInvocationOptions()
        {
            ErrorPatterns = ToolInvocationOptions.Default.ErrorPatterns.Add( new Regex( @"Markup failed" ) ),
            WarningPatterns = ToolInvocationOptions.Default.WarningPatterns,
            SilentPatterns =
                settings.Verbosity == Verbosity.Detailed
                    ? ImmutableArray<Regex>.Empty
                    : ImmutableArray.Create( new Regex( @": info :" ), new Regex( @"\]Info:" ) ),
            ReplacePatterns = ImmutableArray.Create(

                // Replace pattern when the path is present but not the line
                new ReplacePattern(
                    new Regex( @"(?<time>\[[^\]]*\])(?<severity>\w+):(?<component>\[[^\]]*\])\((~\/)?(?<path>[^\)#]+)\)(?<message>.*)$" ),
                    match => $"{match.Groups["path"]}: {match.Groups["severity"].Value.ToLowerInvariant()}: {match.Groups["message"]}" ),

                // Replace pattern when the path is present including the line.
                new ReplacePattern(
                    new Regex( @"(?<time>\[[^\]]*\])(?<severity>\w+):(?<component>\[[^\]]*\])\((~\/)?(?<path>[^\)#]+)#L(?<line>\d+)\)(?<message>.*)$" ),
                    match
                        => $"{match.Groups["path"]}({match.Groups["line"]}): {match.Groups["severity"].Value.ToLowerInvariant()}: {match.Groups["message"]}" ),

                // Replace pattern without the path
                new ReplacePattern(
                    new Regex( @"(?<time>\[[^\]]*\])(?<severity>\w+):(?<component>\[[^\]]*\])\((~\/)?(?<message>.*)$" ),
                    match => $"{this.SolutionPath}: {match.Groups["severity"].Value.ToLowerInvariant()}: {match.Groups["message"]}" ) )
        };

        const string docfxPackageName = "docfx.console";
        var docfxPackagesDirectories = Directory.GetDirectories( Path.Combine( context.RepoDirectory, "docfx\\packages" ), $"{docfxPackageName}.*" );

        if ( docfxPackagesDirectories.Length == 0 )
        {
            context.Console.WriteError( $"The {docfxPackageName} package has not been restored." );

            return false;
        }

        if ( docfxPackagesDirectories.Length > 1 )
        {
            context.Console.WriteError( $"More than one version of the {docfxPackageName} package has been restored." );

            return false;
        }

        var docfxPackageDirectory = docfxPackagesDirectories[0];

        return ToolInvocationHelper.InvokeTool(
            context.Console,
            Path.Combine( docfxPackageDirectory, "tools\\docfx.exe" ),
            Path.Combine( context.RepoDirectory, this.SolutionPath ),
            context.RepoDirectory,
            options );
    }

    public override bool Pack( BuildContext context, BuildSettings settings )
    {
        if ( !this.Build( context, settings ) )
        {
            return false;
        }

        var zipPath = Path.Combine( context.RepoDirectory, $"artifacts\\publish\\private\\{this._archiveName}" );

        if ( File.Exists( zipPath ) )
        {
            File.Delete( zipPath );
        }

        ZipFile.CreateFromDirectory(
            Path.Combine( context.RepoDirectory, "docfx\\_site" ),
            zipPath );

        return true;
    }

    public override bool Test( BuildContext context, BuildSettings settings )
    {
        return true;
    }

    public override bool Restore( BuildContext context, BuildSettings settings )
    {
        return DotNetHelper.Run( context, settings, Path.Combine( context.RepoDirectory, "docfx\\DocFx.csproj" ), "restore" );
    }
}