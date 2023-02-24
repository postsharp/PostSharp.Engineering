// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Spectre.Console;
using System;
using System.Collections.Concurrent;

namespace PostSharp.Engineering.BuildTools.Utilities;

internal class BufferingConsoleHelper : ConsoleHelper
{
    private readonly ConcurrentQueue<Action> _queue;

    public void Replay()
    {
        while ( this._queue.TryDequeue( out var result ) )
        {
            result();
        }
    }

    private BufferingConsoleHelper( IAnsiConsole outConsole, IAnsiConsole errorConsole, ConcurrentQueue<Action> queue ) : base( outConsole, errorConsole )
    {
        this._queue = queue;
    }

    public static BufferingConsoleHelper Create( ConsoleHelper underlying )
    {
        var queue = new ConcurrentQueue<Action>();

        return new BufferingConsoleHelper( new BufferingAnsiConsole( underlying.Out, queue ), new BufferingAnsiConsole( underlying.Error, queue ), queue );
    }
}