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