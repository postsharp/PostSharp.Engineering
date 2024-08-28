// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PostSharp.Engineering.DocFx;

public abstract class DocFxSolutionBase : Solution
{
    private readonly string _command;

    protected DocFxSolutionBase( string solutionPath, string command ) : base( solutionPath )
    {
        this._command = command;
    }

    public override string Name => Path.GetFileName( this.SolutionPath ) + ":" + this._command;

    public sealed override bool Build( BuildContext context, BuildSettings settings )
    {
        var options = new ToolInvocationOptions()
        {
            ErrorPatterns = ToolInvocationOptions.Default.ErrorPatterns.Add( new Regex( @"Markup failed" ) ),
            WarningPatterns = ToolInvocationOptions.Default.WarningPatterns.Add( new Regex( "^warning:" ) ),
            SilentPatterns =
                settings.Verbosity == Verbosity.Detailed
                    ? ImmutableArray<Regex>.Empty
                    : [new Regex( @": info :" ), new Regex( @"\]Info:" )],
            ReplacePatterns =
            [
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
                    match => $"{this.SolutionPath}: {match.Groups["severity"].Value.ToLowerInvariant()}: {match.Groups["message"]}" )
            ]
        };

        var entryPointPath = Assembly.GetEntryAssembly() ?? throw new FileNotFoundException( "Cannot find the entry assembly." );

        return DotNetInvocationHelper.Run(
            context,
            $"{entryPointPath.Location} docfx {this._command} --nologo",
            Path.Combine( context.RepoDirectory, this.SolutionPath ),
            options );
    }

    public override bool Pack( BuildContext context, BuildSettings settings ) => true;

    public override bool Test( BuildContext context, BuildSettings settings ) => true;

    public override bool Restore( BuildContext context, BuildSettings settings ) => true;
}