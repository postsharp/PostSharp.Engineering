// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;

#pragma warning disable CA1416 // Available on Windows only.

namespace PostSharp.Engineering.BuildTools.Build
{
    /// <summary>
    /// Kills all processes that may lock build artefacts.
    /// </summary>
    [UsedImplicitly]
    public class KillCommand : BaseCommand<KillCommandSettings>
    {
        protected override bool ExecuteCore( BuildContext context, KillCommandSettings settings ) => ProcessKiller.Kill( context.Console, settings.Dry );
    }
}