// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public static class VerbosityExtensions
    {
        public static string ToAlias( this Verbosity verbosity )
            => verbosity switch
            {
                Verbosity.Minimal => "m",
                Verbosity.Detailed => "detailed",
                Verbosity.Diagnostic => "diag",
                Verbosity.Standard => "s",
                _ => throw new ArgumentOutOfRangeException( nameof(verbosity), verbosity, null )
            };
    }
}