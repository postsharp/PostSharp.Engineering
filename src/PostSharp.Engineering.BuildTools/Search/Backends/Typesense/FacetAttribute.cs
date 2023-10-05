// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;

namespace PostSharp.Engineering.BuildTools.Search.Backends.Typesense;

[AttributeUsage( AttributeTargets.Property )]
public class FacetAttribute : Attribute { }