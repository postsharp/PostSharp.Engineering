using System;

namespace PostSharp.Engineering.BuildTools.Build.Model;

public class PrepareCompletedEventArgs : EventArgs
{
    public BuildContext Context { get; }

    public BaseBuildSettings Settings { get; }

    public bool IsFailed { get; set; }

    internal PrepareCompletedEventArgs( BuildContext context, BaseBuildSettings settings )
    {
        this.Context = context;
        this.Settings = settings;
    }
}