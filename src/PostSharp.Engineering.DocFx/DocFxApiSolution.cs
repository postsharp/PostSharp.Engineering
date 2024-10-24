﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;

namespace PostSharp.Engineering.DocFx;

[PublicAPI]
public class DocFxApiSolution : DocFxSolutionBase
{
    public DocFxApiSolution( string solutionPath ) : base( solutionPath, "metadata" )
    {
        this.BuildMethod = BuildTools.Build.Model.BuildMethod.Build;
    }
}