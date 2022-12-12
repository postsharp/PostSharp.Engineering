using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;

namespace PostSharp.Engineering.BuildTools.CodeStyle;

public abstract class ResharperCommand : BaseCommand<CommonCommandSettings>
{
   
    protected abstract string Title { get; }

    protected abstract string GetCommand( BuildContext context, Solution solution );
    protected sealed override bool ExecuteCore( BuildContext context, CommonCommandSettings settings )
    {
         
        context.Console.WriteHeading( this.Title );

        if ( !VcsHelper.CheckNoChange( context, settings, context.RepoDirectory ) )
        {
            return false;
        }

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
                    
                var command = this.GetCommand( context, solution ).Trim();

                // Exclude .nuget directory to prevent formatting the code inside NuGet packages.
                command += " --exclude:\"**\\.nuget\\**\\*";

                if ( solution.FormatExclusions is { Length: > 0 } )
                {
                    command += $";{string.Join( ';', solution.FormatExclusions )}\"";
                }

                // Sets MSBuild property 'FormattingCode' to true during code formatting, this allows for exclusion of specific projects from Compile target when code formatting is run.
                command += $" --properties:FormattingCode=true";
                    
                // This is to force the tool to use a specific version of the .NET SDK. It does not work without that.
                command += " --dotnetcoresdk=7.0.100 --toolset=17.4";

                command += " --verbosity=WARN";

                if ( !DotNetTool.Resharper.Invoke( context, command ) )
                {
                    return false;
                }
            }
        }

        context.Console.WriteSuccess( $"{this.Title} was successful." );

        return true;
    }
}