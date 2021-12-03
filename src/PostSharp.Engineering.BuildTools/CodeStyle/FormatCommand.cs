﻿using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Utilities;

namespace PostSharp.Engineering.BuildTools.CodeStyle
{
    public class FormatCommand : BaseCommand<BaseCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BaseCommandSettings options )
        {
            context.Console.WriteHeading( "Reformatting the code" );

            if ( !CheckNoChange( context, options, context.RepoDirectory ) )
            {
                return false;
            }

            foreach ( var solution in context.Product.Solutions )
            {
                if ( solution.CanFormatCode )
                {
                    var command = $"cleanupcode --profile:Custom {solution.SolutionPath} --disable-settings-layers:\"GlobalAll;GlobalPerProduct;SolutionPersonal;ProjectPersonal\"";

                    if ( !solution.FormatExclusions.IsDefaultOrEmpty )
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