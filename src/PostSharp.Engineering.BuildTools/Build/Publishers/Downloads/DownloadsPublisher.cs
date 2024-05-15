// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.S3.Publishers;
using System.Collections.Generic;

namespace PostSharp.Engineering.BuildTools.Build.Publishers.Downloads;

[PublicAPI]
public class DownloadsPublisher : InvalidatingS3Publisher
{
    public DownloadsPublisher( IReadOnlyCollection<S3PublisherConfiguration> configurations ) : base(
        configurations,
        "DOWNLOADS_API_KEY",
        "https://www.postsharp.net/download/Refresh.ashx?p={0}" ) { }
}