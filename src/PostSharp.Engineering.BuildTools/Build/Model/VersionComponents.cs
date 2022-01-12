namespace PostSharp.Engineering.BuildTools.Build.Model;

public record VersionComponents(
    string MainVersion,
    string VersionPrefix,
    int PatchNumber,
    string VersionSuffix );