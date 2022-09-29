// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Collections.Immutable;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    public static class Dependencies
    {
        public static DependencyDefinition MetalamaCompiler { get; } = new(
            "Metalama.Compiler",
            VcsProvider.AzureRepos,
            "Metalama" )
        {
            EngineeringDirectory = "eng-Metalama",

            // The release build is intentionally used for the debug configuration because we want dependencies to consume the release
            // build, for performance reasons. The debug build will be used only locally, and for this we don't need a configuration here.
            CiBuildTypes = new ConfigurationSpecific<string>(
                "Metalama_MetalamaCompiler_ReleaseBuild",
                "Metalama_MetalamaCompiler_ReleaseBuild",
                "Metalama_MetalamaCompiler_PublicBuild" )
        };

        public static DependencyDefinition Metalama { get; } = new(
            "Metalama",
            VcsProvider.AzureRepos,
            "Metalama" );

        public static DependencyDefinition MetalamaSamples { get; } =
            new( "Metalama.Samples", VcsProvider.GitHub, "Metalama", false ) { CodeStyle = "Metalama.Samples" };

        public static DependencyDefinition MetalamaDocumentation { get; } = new( "Metalama.Documentation", VcsProvider.GitHub, "Metalama", false );

        public static DependencyDefinition MetalamaTry { get; } =
            new( "Metalama.Try", VcsProvider.AzureRepos, "Metalama", false ) { EngineeringDirectory = "eng-Metalama" };

        public static DependencyDefinition MetalamaVsx { get; } = new( "Metalama.Vsx", VcsProvider.AzureRepos, "Metalama" );

        public static DependencyDefinition MetalamaOpenAutoCancellationToken { get; } =
            new( "Metalama.Open.AutoCancellationToken", VcsProvider.GitHub, "Metalama.MetalamaOpen" );

        public static DependencyDefinition MetalamaOpenCostura { get; } =
            new( "Metalama.Open.Costura", VcsProvider.GitHub, "Metalama.MetalamaOpen" );

        public static DependencyDefinition MetalamaOpenVirtuosity { get; } =
            new( "Metalama.Open.Virtuosity", VcsProvider.GitHub, "Metalama.MetalamaOpen" );

        public static DependencyDefinition MetalamaFrameworkExtensions { get; } =
            new( "Metalama.Framework.Extensions", VcsProvider.GitHub, "Metalama" );

        // This is only used from the project template.
        public static DependencyDefinition MyProduct { get; } =
            new( "PostSharp.Engineering.ProjectTemplate", VcsProvider.GitHub, "NONE" );

        public static DependencyDefinition PostSharpEngineering { get; } = new(
            "PostSharp.Engineering",
            VcsProvider.GitHub,
            "postsharp" )
        {
            GenerateSnapshotDependency = false,

            // We always use the debug build for engineering.
            CiBuildTypes = new ConfigurationSpecific<string>(
                "PostSharpEngineering_DebugBuild",
                "PostSharpEngineering_DebugBuild",
                "PostSharpEngineering_DebugBuild" )
        };

        [Obsolete( "Renamed to MetalamaBackstage" )]
        public static DependencyDefinition PostSharpBackstageSettings { get; } = new(
            "PostSharp.Backstage.Settings",
            VcsProvider.AzureRepos,
            "Metalama" );

        public static DependencyDefinition MetalamaBackstage { get; } = new(
            "Metalama.Backstage",
            VcsProvider.AzureRepos,
            "Metalama" );

        public static DependencyDefinition BusinessSystems { get; } = new(
            "BusinessSystems",
            VcsProvider.AzureRepos,
            "WebsitesAndBusinessSystems",
            false );

        public static DependencyDefinition HelpBrowser { get; } = new(
            "HelpBrowser",
            VcsProvider.AzureRepos,
            "WebsitesAndBusinessSystems",
            false );

        public static DependencyDefinition PostSharpWeb { get; } = new(
            "PostSharpWeb",
            VcsProvider.AzureRepos,
            "WebsitesAndBusinessSystems",
            false );

        public static ImmutableArray<DependencyDefinition> All { get; } = ImmutableArray.Create(
            MetalamaCompiler,
            Metalama,
            MetalamaDocumentation,
            MetalamaSamples,
            MetalamaTry,
            MetalamaVsx,
            MetalamaOpenAutoCancellationToken,
            MetalamaOpenCostura,
            MetalamaOpenVirtuosity,
            MetalamaFrameworkExtensions,
            PostSharpEngineering,
            MetalamaBackstage,
            BusinessSystems,
            HelpBrowser,
            PostSharpWeb );
    }
}