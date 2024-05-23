﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;

namespace PostSharp.Engineering.BuildTools.Docker;

internal static class DockerHelper
{
    public static bool IsDockerBuild()
    {
        var s = Environment.GetEnvironmentVariable( "COMPLUS_RUNNING_IN_CONTAINER" );

        return s != null && bool.TryParse( s, out var b ) && b;
    }
}