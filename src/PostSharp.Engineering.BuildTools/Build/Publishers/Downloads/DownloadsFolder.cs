// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Build.Publishers.Downloads;

[PublicAPI]
public record DownloadsFolder(
    string Name,
    string Order,
    DateTime CreatedAt,
    string? Description,
    string? LongDescription,
    string? Instructions,
    IEnumerable<DownloadsFile> Files )
{
    public static DownloadsFolder Create(
        BuildContext context,
        BuildInfo buildInfo,
        string? description = null,
        string? longDescription = null,
        string? instructions = null,
        IEnumerable<(string Directory, ParametricString Name, string? Description, string? Instructions)>? files = null )
    {
        var familyName = context.Product.ProductFamily.Name;
        var packageVersion = buildInfo.PackageVersion!;
        var name = $"{familyName} {packageVersion}";
        var order = packageVersion.Split( '-' )[0];
        var createdAt = DateTime.UtcNow;

        var downloadsFiles =
            files?.Select( f => DownloadsFile.Create( Path.Combine( f.Directory, f.Name.ToString( buildInfo ) ), f.Description, f.Instructions ) )
            ?? Enumerable.Empty<DownloadsFile>();
        
        return new DownloadsFolder( name, order, createdAt, description, longDescription, instructions, downloadsFiles );
    }
    
    public DownloadsFolder WithFiles( IEnumerable<DownloadsFile> files ) => this with { Files = files };
}