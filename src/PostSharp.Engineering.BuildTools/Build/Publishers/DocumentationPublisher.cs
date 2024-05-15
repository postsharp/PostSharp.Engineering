// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.S3.Publishers;
using System.Collections.Generic;

namespace PostSharp.Engineering.BuildTools.Build.Publishers;

[PublicAPI]
public class DocumentationPublisher : InvalidatingS3Publisher
{
    private readonly string _documentationUrl;

    public DocumentationPublisher( IReadOnlyCollection<S3PublisherConfiguration> configurations, string documentationUrl )
        : base( configurations, "DOC_API_KEY", $"{documentationUrl}_api/invalidate?{{0}}" )
    {
        this._documentationUrl = documentationUrl;
    }

    public override SuccessCode PublishFile( BuildContext context, PublishSettings settings, string file, BuildInfo buildInfo, BuildConfigurationInfo configuration ) => base.PublishFile( context, settings, file, buildInfo, configuration );
}