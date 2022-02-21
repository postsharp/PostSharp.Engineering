using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Utilities;
using System.IO;

namespace PostSharp.Engineering.BuildTools.CodeStyle
{
    /// <summary>
    /// Formats the source code of the current repo by applying the standard code style (running JetBrains cleanupcode command).
    /// </summary>
    public class FormatCommand : BaseCommand<CommonCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, CommonCommandSettings settings )
        {
            context.Console.WriteHeading( "Reformatting the code" );

            if ( !VcsHelper.CheckNoChange( context, settings, context.RepoDirectory ) )
            {
                return false;
            }

            foreach ( var solution in context.Product.Solutions )
            {
                if ( solution.CanFormatCode )
                {
                    var command =
                        $"cleanupcode --profile:Custom \"{Path.Combine( context.RepoDirectory, solution.SolutionPath )}\" --disable-settings-layers:\"GlobalAll;GlobalPerProduct;SolutionPersonal;ProjectPersonal\"";

                    if ( solution.FormatExclusions is { Length: > 0 } )
                    {
                        command += $" --exclude:\"{string.Join( ';', solution.FormatExclusions )}\"";
                    }

                    if ( !DotNetTool.Resharper.Invoke( context, command ) )
                    {
                        return false;
                    }
                }
            }

            context.Console.WriteSuccess( "Reformatting the code was successful." );

            return true;
        }
    }
}