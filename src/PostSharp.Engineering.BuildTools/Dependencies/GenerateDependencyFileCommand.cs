using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Dependencies
{
    public class GenerateDependencyFileCommand : BaseCommand<GenerateDependencyFileSettings>
    {
        protected override bool ExecuteCore( BuildContext context, GenerateDependencyFileSettings options )
        {
            context.Console.WriteHeading( "Setting the local dependencies" );

            if ( context.Product.Dependencies.IsDefaultOrEmpty )
            {
                context.Console.WriteError( "This product has no dependency." );

                return false;
            }

            if ( options.Repos.Length == 0 && !(options.All || options.None) )
            {
                context.Console.WriteError( "No dependency was specified. Use --all or --none." );

                return false;
            }

            var versionsOverridePath = Path.Combine(
                context.RepoDirectory,
                context.Product.EngineeringDirectory,
                "Versions.g.props" );

            XDocument versionsOverrideDocument;
            List<(XElement XElement, string? Project)> imports;

            if ( File.Exists( versionsOverridePath ) )
            {
                versionsOverrideDocument = XDocument.Load( versionsOverridePath );

                imports = versionsOverrideDocument.Root!.Elements( "Import" )
                    .Select( i => (XElement: i, Project: i.Attribute( "Project" )?.Value) )
                    .Where( i => i.Project != null )
                    .Where( i => i.Project!.EndsWith( ".Import.props" ) && !i.Project.EndsWith( context.Product.ProductNameWithoutDot + ".Import.props" ) )
                    .ToList();

                // Remove all imports. We will put them back later.
                foreach ( var import in imports )
                {
                    import.XElement.Remove();
                }
            }
            else
            {
                versionsOverrideDocument = new XDocument( new XElement( "Project" ) );
                imports = new List<(XElement XElement, string? Project)>();
            }

            if ( options.None )
            {
                if ( imports.Count == 0 )
                {
                    context.Console.WriteMessage( "Nothing to do." );

                    return true;
                }
            }
            else
            {
                var localRepos = options.All ? context.Product.Dependencies.Select( x => x.Name ) : options.Repos;

                foreach ( var localDependency in localRepos )
                {
                    ProductDependency? dependency;

                    if ( int.TryParse( localDependency, out var index ) )
                    {
                        if ( index < 1 || index > context.Product.Dependencies.Length )
                        {
                            context.Console.WriteError( $"'{index}' is not a valid dependency index. Use the 'dependencies list' command." );

                            return false;
                        }

                        dependency = context.Product.Dependencies[index - 1];
                    }
                    else
                    {
                        dependency = context.Product.Dependencies.FirstOrDefault(
                            d =>
                                d.Name.Equals( localDependency, StringComparison.OrdinalIgnoreCase ) );

                        if ( dependency == null )
                        {
                            context.Console.WriteError(
                                $"'{localDependency}' is not a valid dependency name for this product. Use the 'dependencies list' command." );

                            return false;
                        }
                    }

                    var importProjectFile = Path.GetFullPath(
                        Path.Combine(
                            context.RepoDirectory,
                            "..",
                            dependency.Name,
                            dependency.Name + ".Import.props" ) );

                    if ( !File.Exists( importProjectFile ) )
                    {
                        context.Console.WriteError( $"The file '{importProjectFile}' does not exist. Make sure the dependency repo is built." );

                        return false;
                    }

                    versionsOverrideDocument.Root!.Add( new XElement( "Import", new XAttribute( "Project", importProjectFile ) ) );
                }

                context.Console.WriteImportantMessage( $"Writing '{versionsOverridePath}'" );
                context.Console.WriteMessage( versionsOverrideDocument.ToString() );
                versionsOverrideDocument.Save( versionsOverridePath );
            }

            context.Console.WriteSuccess( "Setting local dependencies was successful." );

            return true;
        }
    }
}