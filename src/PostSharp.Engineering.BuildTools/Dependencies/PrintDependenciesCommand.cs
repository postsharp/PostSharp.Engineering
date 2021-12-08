using PostSharp.Engineering.BuildTools.Build;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    public class PrintDependenciesCommand : BaseCommand<BaseCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, BaseCommandSettings settings )
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