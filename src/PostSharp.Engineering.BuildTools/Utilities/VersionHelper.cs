﻿using System.Linq;
using System.Reflection;

namespace PostSharp.Engineering.BuildTools.Utilities
{
    public static class VersionHelper
    {
        public static string? EngineeringVersion
            => typeof(BaseCommand<>).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault( a => a.Key == "PackageVersion" )?.Value;
    }
}