// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Microsoft.Build.Locator;
using System;
using System.Runtime.InteropServices;

namespace PostSharp.Engineering.BuildTools;

// ReSharper disable once InconsistentNaming
internal static class MSBuildInitializer
{
    private static bool _isInitialized;

    public static void Initialize()
    {
        if ( !_isInitialized )
        {
            if ( MSBuildLocator.CanRegister )
            {
                try
                {
                    MSBuildLocator.RegisterDefaults();
                    
                    _isInitialized = true;
                }
                catch ( Exception e )
                {
                    throw new InvalidOperationException(
                        $"Cannot find a suitable version of MSBuild for "
                        + $"{RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}. "
                        + "You should probably install an SDK for this .NET version."
                        + "Try this: `winget install Microsoft.DotNet.Sdk.6`.",
                        e );
                }
            }
        }
    }
}