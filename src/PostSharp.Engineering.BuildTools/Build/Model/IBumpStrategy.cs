﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Diagnostics.CodeAnalysis;

namespace PostSharp.Engineering.BuildTools.Build.Model;

public interface IBumpStrategy
{
    bool TryBumpVersion(
        Product product,
        BuildContext context,
        [NotNullWhen( true )] out Version? oldVersion,
        [NotNullWhen( true )] out Version? newVersion );
}