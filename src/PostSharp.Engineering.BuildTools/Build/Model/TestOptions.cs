// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Build.Model;

public class TestOptions
{
    public bool IgnoreExitCode { get; set; }

    public string[]? ErrorRegexes { get; set; }

    public string[]? SuccessRegexes { get; set; }
}