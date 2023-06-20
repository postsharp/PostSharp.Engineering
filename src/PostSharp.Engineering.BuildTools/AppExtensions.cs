// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.CodeStyle;
using PostSharp.Engineering.BuildTools.ContinuousIntegration;
using PostSharp.Engineering.BuildTools.Csproj;
using PostSharp.Engineering.BuildTools.Dependencies;
using PostSharp.Engineering.BuildTools.Git;
using PostSharp.Engineering.BuildTools.NuGet;
using PostSharp.Engineering.BuildTools.Search;
using PostSharp.Engineering.BuildTools.Tools;
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

                        root.AddCommand<GenerateCiScriptsCommand>( "generate-scripts" )
                            .WithData( product )
                            .WithDescription( "Generates the continuous integration scripts" );

                        root.AddCommand<ListSolutionsCommand>( "list-solutions" )
                            .WithData( product )
                            .WithDescription( "Lists the solutions in the build sequence." );

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

                        if ( product.DependencyDefinition.IsVersioned )
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
                                    .WithDescription( "Fetch build dependencies from TeamCity but does not update a version that has already been resolved." );

                                dependencies.AddCommand<UpdateDependencyCommand>( "update" )
                                    .WithData( product )
                                    .WithDescription( "Updates dependencies to the newest version available on TeamCity." );

                                dependencies.AddCommand<UpdateEngineeringCommand>( "update-eng" )
                                    .WithData( product )
                                    .WithDescription( "Updates PostSharp.Engineering in global.json and Versions.props." );
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

                                    codestyle.AddCommand<InspectCommand>( "inspect" )
                                        .WithData( product )
                                        .WithDescription( "Inspects the code for warnings" );

                                    codestyle.AddCommand<ProcessInspectOutputCommand>( "process-inspect-output" )
                                        .WithData( product )
                                        .WithDescription( "Prints errors and warnings for the output of the 'inspect' command" );
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

                                        nuget.AddCommand<UnlistNugetPackageCommand>( "unlist" )
                                            .WithDescription( "Unlists package published on nuget.org." );
                                    } );

                                tools.AddBranch(
                                    "git",
                                    git =>
                                    {
                                        git.AddCommand<GitBulkRenameCommand>( "rename" )
                                            .WithDescription( "Renames all files and directories recursively preserving GIT history." )
                                            .WithExample( new[] { @"""C:\src\Caravela.Compiler""", @"""Caravela""", @"""Metalama""" } );
                                        
                                        git.AddCommand<DownstreamMergeCommand>( "merge-downstream" )
                                            .WithData( product )
                                            .WithDescription( "Merges the code to the subsequent development branch." );

                                        git.AddCommand<UpstreamCheckCommand>( "check-upstream" )
                                            .WithData( product )
                                            .WithDescription( "Checks the upstream product versions for unmerged changes." );
                                    } );

                                tools.AddBranch(
                                    "xmldoc",
                                    xmldoc => xmldoc.AddCommand<RemoveInternalsCommand>( "clean" ).WithDescription( "Remove internals." ).WithData( product ) );

                                tools.AddBranch(
                                    "search",
                                    search =>
                                    {
                                        search.AddCommand<UpdateSearchCommand>( "update" )
                                            .WithDescription(
                                                "Updates a search collection from the given source or writes data retrieved by a crawler to the console when --dry option is used." )
                                            .WithExample(
                                                new[]
                                                {
                                                    "metalamadoc http://localhost:8108 https://doc.example.com/sitemap.xml",
                                                    "metalamadoc http://localhost:8108 https://doc.example.com/conceptual/tryme --page --dry"
                                                } );
                                    } );

                                foreach ( var tool in product.DotNetTools )
                                {
                                    tools.AddCommand<InvokeDotNetToolCommand>( tool.Alias )
                                        .WithData( product )
                                        .WithDescription( $"Execute dot net tool '{tool.Command}' from package '{tool.PackageId}' version {tool.Version}." );
                                }
                            } );

                        root.AddBranch(
                            "teamcity",
                            teamcity =>
                            {
                                teamcity.AddCommand<TeamCityBuildCommand>( "run" )
                                    .WithData( product )
                                    .WithDescription( "Triggers specified build type of specified product on TeamCity." );

                                teamcity.AddBranch(
                                    "project",
                                    project =>
                                    {
                                        project.AddCommand<TeamCityGetProjectDetailsCommand>( "get" )
                                            .WithData( product )
                                            .WithDescription( "Get details of a TeamCity project." );

                                        project.AddCommand<TeamCityCreateProjectCommand>( "create" )
                                            .WithData( product )
                                            .WithDescription( "Creates a new TeamCity project." );

                                        project.AddCommand<TeamCityCreateThisProjectCommand>( "create-this" )
                                            .WithData( product )
                                            .WithDescription(
                                                "Creates a new TeamCity project and VCS root, if it doesn't exist, based on the product in the current repository." );
                                    } );

                                teamcity.AddBranch(
                                    "vcs-root",
                                    vcsRoot =>
                                    {
                                        vcsRoot.AddCommand<TeamCityGetVcsRootDetailsCommand>( "get" )
                                            .WithData( product )
                                            .WithDescription( "Get details of a TeamCity VCS root." );

                                        vcsRoot.AddCommand<TeamCityCreateThisVcsRootCommand>( "create-this" )
                                            .WithData( product )
                                            .WithDescription(
                                                "Creates a new TeamCity VCS root, if it doesn't exist, based on the product in the current repository, in a specified project." );
                                    } );
                            } );
                    } );
            }
        }
    }
}