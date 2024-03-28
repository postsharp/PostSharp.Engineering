// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using NuGet.Versioning;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System.IO;

namespace PostSharp.Engineering.BuildTools.CodeStyle;

internal abstract class ResharperCommand : BaseCommand<CommonCommandSettings>
{
    protected abstract string Title { get; }

    protected abstract string GetCommand( BuildContext context, Solution solution );

    protected virtual void OnSuccessfulExecution( BuildContext context, Solution solution ) { }

    protected override bool ExecuteCore( BuildContext context, CommonCommandSettings settings )
    {
        context.Console.WriteHeading( this.Title );

        var buildSettings = new BuildSettings();
        buildSettings.Initialize( context );

        foreach ( var solution in context.Product.Solutions )
        {
            if ( solution.CanFormatCode )
            {
                // Before formatting, the solution must be built.
                if ( !solution.Build( context, buildSettings ) )
                {
                    return false;
                }

                // Determine the current SDK.
                ToolInvocationHelper.InvokeTool( context.Console, "dotnet", "--version", Path.GetDirectoryName( solution.SolutionPath ), out _, out var sdkVersionString );

                if ( !NuGetVersion.TryParse( sdkVersionString, out var sdkVersion ) )
                {
                    context.Console.WriteError( $"Cannot parse Sdk version '{sdkVersionString}'." );

                    return false;
                }

                var command = this.GetCommand( context, solution ).Trim();

                // Exclude user- and machine-specific layers.
                command += " --disable-settings-layers:\"GlobalAll;GlobalPerProduct;SolutionPersonal;ProjectPersonal\"";

                // Exclude .nuget directory to prevent formatting the code inside NuGet packages.
                command += " --exclude:\"**\\.nuget\\**\\*";

                if ( solution.FormatExclusions is { Length: > 0 } )
                {
                    command += $";{string.Join( ';', solution.FormatExclusions )}";
                }

                command += "\"";
                
                // This is to force the tool to use a specific version of the .NET SDK. It does not work without that.
                // ReSharper disable once StringLiteralTypo
                command += $" --dotnetcoresdk={sdkVersion.ToFullString()}";

                command += " --verbosity=WARN";

                if ( !DotNetTool.Resharper.Invoke( context, command ) )
                {
                    return false;
                }

                this.OnSuccessfulExecution( context, solution );
            }
        }

        context.Console.WriteSuccess( $"{this.Title} was successful." );

        return true;
    }
}