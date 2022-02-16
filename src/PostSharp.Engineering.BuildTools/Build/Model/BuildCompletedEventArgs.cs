using System;

namespace PostSharp.Engineering.BuildTools.Build.Model;

public class BuildCompletedEventArgs : EventArgs
{
    public BuildContext Context { get; }

    public BuildSettings Settings { get; }

    public string PrivateArtifactsDirectory { get; }

    public bool IsFailed { get; set; }

    internal BuildCompletedEventArgs( BuildContext context, BuildSettings settings, string privateArtifactsDirectory )
    {
        this.Context = context;
        this.Settings = settings;
        this.PrivateArtifactsDirectory = privateArtifactsDirectory;
    }
}