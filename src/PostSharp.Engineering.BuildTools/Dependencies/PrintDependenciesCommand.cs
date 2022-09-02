// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    /// <summary>
    /// Prints the content of <c>Versions.g.props</c> to the console.
    /// </summary>
    public class PrintDependenciesCommand : BaseCommand<CommonCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, CommonCommandSettings settings )
        {
            var path = Path.Combine(
                context.RepoDirectory,
                context.Product.EngineeringDirectory,
                "Versions.g.props" );

            if ( File.Exists( path ) )
            {
                context.Console.WriteImportantMessage( $"'{path}' has the following content:" );
                context.Console.WriteMessage( File.ReadAllText( path ) );
            }
            else
            {
                context.Console.WriteWarning( $"The file '{path}' does not exist. There are no local dependencies." );
            }

            return true;
        }
    }
}