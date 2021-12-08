// Copyright (c) SharpCrafters s.r.o. All rights reserved. Released under the MIT license.

using PostSharp.Engineering.BuildTools;
using PostSharp.Engineering.BuildTools.Build.Model;
using Spectre.Console.Cli;
using System.Collections.Immutable;

// These packages are published to internal and private feeds.
var product = new Product
{
    ProductName = "PostSharp.Engineering",
    Solutions = ImmutableArray.Create<Solution>( new DotNetSolution( "PostSharp.Engineering.sln" ) { SupportsTestCoverage = true, CanFormatCode = true } ),
    PublicArtifacts = Pattern.Create( "PostSharp.Engineering.Sdk.$(PackageVersion).nupkg", "PostSharp.Engineering.BuildTools.$(PackageVersion).nupkg" ),
    RequiresEngineeringSdk = false
};

var commandApp = new CommandApp();

commandApp.AddProductCommands( product );

return commandApp.Run( args );