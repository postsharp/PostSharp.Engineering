// Copyright (c) SharpCrafters s.r.o. All rights reserved. Released under the MIT license.

using PostSharp.Engineering.BuildTools;
using PostSharp.Engineering.BuildTools.Build.Model;
using Spectre.Console.Cli;
using System.Collections.Immutable;

var privateSource = new NugetSource( "%INTERNAL_NUGET_PUSH_URL%", "%INTERNAL_NUGET_API_KEY%" );

var publicSource = new NugetSource( "https://api.nuget.org/v3/index.json", "%NUGET_ORG_API_KEY%" );

// These packages are published to internal and private feeds.
var publicPackages = new ParametricString[] { "PostSharp.Engineering.Sdk.$(PackageVersion).nupkg", "PostSharp.Engineering.BuildTools.$(PackageVersion).nupkg" };

var publicPublishing = new NugetPublishTarget(
    Pattern.Empty.Add( publicPackages ),
    privateSource,
    publicSource );

var product = new Product
{
    ProductName = "PostSharp.Engineering",
    Solutions = ImmutableArray.Create<Solution>( new DotNetSolution( "PostSharp.Engineering.sln" ) { SupportsTestCoverage = true, CanFormatCode = true } ),
    PublishingTargets = ImmutableArray.Create<PublishingTarget>( publicPublishing ),
    RequiresEngineeringSdk = false
};

var commandApp = new CommandApp();

commandApp.AddProductCommands( product );

return commandApp.Run( args );