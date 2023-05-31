// TODO: Read from MainVersion.props

using System;

public static class MainVersion
{
    public const string Value = "2023.2";

    public static string ValueWithoutDots { get; } = Value.Replace( ".", "", StringComparison.Ordinal );
}