// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using System;

namespace PostSharp.Engineering.BuildTools.Build.Model;

[PublicAPI]
public class PublishEventArgs : EventArgs
{
    public BuildContext Context { get; }

    public PublishSettings Settings { get; }

    public (string Private, string Public) Directories { get; }

    public BuildConfigurationInfo Configuration { get; }

    public BuildInfo BuildInfo { get; }

    public bool IsPublic { get; }

    public bool Success { get; set; } = true;

    public PublishEventArgs(
        BuildContext context,
        PublishSettings settings,
        (string Private, string Public) directories,
        BuildConfigurationInfo configuration,
        BuildInfo buildInfo,
        bool isPublic )
    {
        this.Context = context;
        this.Settings = settings;
        this.Directories = directories;
        this.Configuration = configuration;
        this.BuildInfo = buildInfo;
        this.IsPublic = isPublic;
    }
}