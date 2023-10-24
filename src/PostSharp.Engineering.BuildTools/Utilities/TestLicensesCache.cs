// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using System;
using System.Collections.Generic;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Utilities;

public static class TestLicensesCache
{
    public static void FetchOnPrepareCompleted( PrepareCompletedEventArgs arg )
    {
        const string visualStudioTenantId = "171276b2-7a8c-4c9b-bc49-57889e2e4f42";
        const string keyVaultUri = "https://testserviceskeyvault.vault.azure.net/";

        if ( BuildContext.IsGuestDevice )
        {
            arg.Context.Console.WriteWarning( "Skipping fetching of test license keys. Some licensing tests are going to fail." );

            return;
        }

        // Should the content of the file change, change the file name, to keep older builds consistent.
        var testLicensesCacheDirectory = Path.Combine( PathHelper.GetMetalamaApplicationDataDirectory(), "TestLicenseKeysCache" );
        var licensesFile = Path.Combine( testLicensesCacheDirectory, "TestLicenseKeys1.g.props" );

        if ( File.Exists( licensesFile ) )
        {
            arg.Context.Console.WriteMessage( "Test license keys are fetched already." );

            return;
        }

        if ( !Directory.Exists( testLicensesCacheDirectory ) )
        {
            Directory.CreateDirectory( testLicensesCacheDirectory );
        }

        arg.Context.Console.WriteHeading( "Fetching test license keys." );
        arg.Context.Console.WriteMessage( "This operation can be lengthy, but its result is cached, and next time it won't need to be performed." );

        var o = new DefaultAzureCredentialOptions()
        {
            // We se the tenant explicitly, to avoid issues where the user is logged in to various tenants at the same time. 
            VisualStudioTenantId = visualStudioTenantId
        };

        var keyVault = new SecretClient( new Uri( keyVaultUri ), new DefaultAzureCredential( o ) );

        var lines = new List<string>();

        lines.Add( "<Project>" );
        lines.Add( "  <PropertyGroup>" );

        var licenseKeyNames = new[]
        {
            "PostSharpEssentials",
            "PostSharpFramework",
            "PostSharpUltimate",
            "PostSharpEnterprise",
            "PostSharpUltimateOpenSourceRedistribution",
            "MetalamaFreePersonal",
            "MetalamaFreeBusiness",
            "MetalamaStarterPersonal",
            "MetalamaStarterBusiness",
            "MetalamaProfessionalPersonal",
            "MetalamaProfessionalBusiness",
            "MetalamaUltimatePersonal",
            "MetalamaUltimateBusiness",
            "MetalamaUltimateBusinessNotAuditable",
            "MetalamaUltimateOpenSourceRedistribution",
            "MetalamaUltimateCommercialRedistribution",
            "MetalamaUltimatePersonalProjectBound",
            "MetalamaUltimateOpenSourceRedistributionForIntegrationTests"
        };

        foreach ( var licenseKeyName in licenseKeyNames )
        {
            string licenseKey;

            try
            {
                licenseKey = keyVault.GetSecret( $"TestLicenseKey{licenseKeyName}" ).Value.Value;
            }
            catch ( Exception ex )
            {
                arg.Context.Console.WriteWarning( $"Could not get license key {licenseKeyName}, some licensing tests are going to fail." );
                arg.Context.Console.WriteMessage( ex.Message );

                return;
            }

            lines.Add( $"    <{licenseKeyName}LicenseKey>{licenseKey}</{licenseKeyName}LicenseKey>" );
        }

        lines.Add( "  </PropertyGroup>" );
        lines.Add( "</Project>" );

        File.WriteAllLines( licensesFile, lines );
        
        arg.Context.Console.WriteMessage( "Test license keys fetched successfully." );
    }
}