namespace PostSharp.Engineering.BuildTools.Build.Model;

public class TestOptions
{
    public bool IgnoreExitCode { get; set; }

    public string[]? OutputRegexes { get; set; }
}