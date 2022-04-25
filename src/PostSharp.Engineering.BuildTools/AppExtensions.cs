using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.CodeStyle;
using PostSharp.Engineering.BuildTools.Csproj;
using PostSharp.Engineering.BuildTools.Dependencies;
using PostSharp.Engineering.BuildTools.Git;
using PostSharp.Engineering.BuildTools.NuGet;
using PostSharp.Engineering.BuildTools.XmlDoc;
using Spectre.Console.Cli;
using System.Linq;

namespace PostSharp.Engineering.BuildTools
{
    public static class AppExtensions
    {
        /// <summary>
        /// Adds <see cref="Product"/>-related commands to a <see cref="CommandApp"/>.
        /// </summary>
        public static void AddProductCommands( this CommandApp app, Product? product = null )
        {
            if ( product != null )
            {
                app.Configure(
                    root =>
                    {
                        root.Settings.StrictParsing = true;

                        root.AddCommand<PrepareCommand>( "prepare" )
                            .WithData( product )
                            .WithDescription( "Creates the files that are required to build the product" );

                        root.AddCommand<BuildCommand>( "build" )
                            .WithData( product )
                            .WithDescription( "Builds all packages in the product (implies 'prepare')" );

                        root.AddCommand<TestCommand>( "test" )
                            .WithData( product )
                            .WithDescription( "Builds all packages then run all tests (implies 'build')" );

                        root.AddCommand<VerifyCommand>( "verify" )
                            .WithData( product )
                            .WithDescription( "Verify that the dependencies of public artifacts have already been publicly deployed" );

                        root.AddCommand<PublishCommand>( "publish" )
                            .WithData( product )
                            .WithDescription( "Publishes all packages that have been previously built by the 'build' command" );

                        root.AddCommand<SwapCommand>( "swap" )
                            .WithData( product )
                            .WithDescription( "Swaps deployment slots" );

                        if ( product.DependencyDefinition.IsVersioned && product.MainVersionDependency == null )
                        {
                            root.AddCommand<BumpCommand>( "bump" )
                                .WithData( product )
                                .WithDescription( "Bumps the version of this product" );
                        }

                        root.AddBranch(
                            "dependencies",
                            dependencies =>
                            {
                                dependencies.AddCommand<ListDependenciesCommand>( "list" )
                                    .WithData( product )
                                    .WithDescription( "Lists the dependencies of this product" );

                                dependencies.AddCommand<SetDependenciesCommand>( "set" )
                                    .WithData( product )
                                    .WithDescription( "Sets how dependencies should be consumed." );

                                dependencies.AddCommand<ResetDependenciesCommand>( "reset" )
                                    .WithData( product )
                                    .WithDescription(
                                        "Resets any change done with the 'set' command and revert to the configuration as stored in source code." );

                                dependencies.AddCommand<PrintDependenciesCommand>( "print" )
                                    .WithData( product )
                                    .WithDescription( "Prints the dependency file." );

                                dependencies.AddCommand<FetchDependencyCommand>( "fetch" )
                                    .WithData( product )
                                    .WithDescription( "Fetch build dependencies from TeamCity." );
                            } );

                        root.AddBranch(
                            "codestyle",
                            codestyle =>
                            {
                                codestyle.AddCommand<PushCodeStyleCommand>( "push" )
                                    .WithData( product )
                                    .WithDescription(
                                        $"Copies the changes in {product.EngineeringDirectory}/shared to the local engineering repo, but does not commit nor push." );

                                codestyle.AddCommand<PullCodeStyleCommand>( "pull" )
                                    .WithData( product )
                                    .WithDescription(
                                        $"Copies the remote engineering repo to {product.EngineeringDirectory}/shared. Automatically pulls 'master'." );

                                if ( product.Solutions.Any( s => s.CanFormatCode ) )
                                {
                                    codestyle.AddCommand<FormatCommand>( "format" )
                                        .WithData( product )
                                        .WithDescription( "Formats the code" );
                                }
                            } );

                        root.AddBranch(
                            "tools",
                            tools =>
                            {
                                tools.AddCommand<KillCommand>( "kill" )
                                    .WithData( product )
                                    .WithDescription( "Kill all compiler processes" );

                                tools.AddBranch(
                                    "csproj",
                                    csproj => csproj.AddCommand<AddProjectReferenceCommand>( "add-project-reference" )
                                        .WithDescription( "Adds a <ProjectReference> item to *.csproj in a directory" ) );

                                tools.AddBranch(
                                    "nuget",
                                    nuget =>
                                    {
                                        nuget.AddCommand<RenamePackagesCommand>( "rename" )
                                            .WithDescription( "Renames all packages in a directory" );

                                        nuget.AddCommand<VerifyPublicPackageCommand>( "verify-public" )
                                            .WithDescription(
                                                "Verifies that all packages in a directory have only references to packages published on nuget.org." );
                                    } );

                                tools.AddBranch(
                                    "git",
                                    git => git.AddCommand<GitBulkRenameCommand>( "rename" )
                                        .WithDescription( "Renames all files and directories recursively preserving GIT history." )
                                        .WithExample( new[] { @"""C:\src\Caravela.Compiler""", @"""Caravela""", @"""Metalama""" } ) );

                                tools.AddBranch(
                                    "xmldoc",
                                    xmldoc => xmldoc.AddCommand<RemoveInternalsCommand>( "clean" ).WithDescription( "Remove internals." ).WithData( product ) );
                            } );

                        root.AddBranch( 
                            "teamcity",
                            teamcity =>
                            {
                                teamcity.AddCommand<TeamCityDeployCommand>( "deploy" )
                                    .WithData( product )
                                    .WithDescription( "Trigger deployment of current product (or specified product) on TeamCity." )
                                    .WithExample( new[] { "deploy" } )
                                    .WithExample( new[] { "deploy", $@"""{product.ProductName}"" [-b|--bump] [-c config]" } );

                                teamcity.AddCommand<TeamCityBumpCommand>( "bump" )
                                    .WithData( product )
                                    .WithDescription( "Trigger version bump of current product (or specified product) on TeamCity." )
                                    .WithExample( new[] { "bump" } )
                                    .WithExample( new[] { "bump", $@"""{product.ProductName}""" } );
                            } );
                    } );
            }
        }
    }
}