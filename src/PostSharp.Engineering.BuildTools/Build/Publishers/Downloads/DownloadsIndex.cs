// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;

namespace PostSharp.Engineering.BuildTools.Build.Publishers.Downloads;

[PublicAPI]
public record DownloadsIndex( DownloadsFolder Folder, string? Name, bool IsPartialIndex )
{
    public string FileName => this.Name == null ? "Index.xml" : $"Index.{this.Name}.xml";
}