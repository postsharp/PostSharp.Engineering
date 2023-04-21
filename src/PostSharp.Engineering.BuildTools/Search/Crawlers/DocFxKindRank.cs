// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Search.Crawlers;

public enum DocFxKindRank
{
    Unknown = 1,
    Api = 2,
    Common = 3,
    Examples = 4,
    Conceptual = 5
}