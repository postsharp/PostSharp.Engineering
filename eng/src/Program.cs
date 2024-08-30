// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Build.Solutions;
using PostSharp.Engineering.BuildTools.Dependencies.Definitions;
using Spectre.Console.Cli;

var product = new Product( DevelopmentDependencies.PostSharpEngineering )
{
    Solutions =
    [
        new DotNetSolution( "PostSharp.Engineering.sln" ) { SupportsTestCoverage = true, CanFormatCode = true }
    ],
    PublicArtifacts = Pattern.Create(
        "PostSharp.Engineering.Sdk.$(PackageVersion).nupkg",
        "PostSharp.Engineering.BuildTools.$(PackageVersion).nupkg",
        "PostSharp.Engineering.DocFx.$(PackageVersion).nupkg" ),
    RequiresEngineeringSdk = false,
    ExportedProperties = { { "Directory.Packages.props", ["DocFxVersion"] } },
    IsPublishingNonReleaseBranchesAllowed = true
};

var app = new EngineeringApp( product );

return app.Run( args );