// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public static class TeamCityServiceMessageProvider
{
    public static void SendImportDataMessage( string type, string path, string flowId )
    {
        var date = DateTimeOffset.Now;
        var timestamp = $"{date:yyyy-MM-dd'T'HH:mm:ss.fff}{date.Offset.Ticks:+;-;}{date.Offset:hhmm}";

        Console.WriteLine(
            "##teamcity[importData type='{0}' path='{1}' flowId='{2}' timestamp='{3}' whenNoDataPublished='error']",
            type,
            path,
            flowId,
            timestamp );
    }
}