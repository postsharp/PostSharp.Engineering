// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Spectre.Console;
using Spectre.Console.Rendering;
using System;
using System.Collections.Concurrent;

namespace PostSharp.Engineering.BuildTools.Utilities;

internal class BufferingAnsiConsole : IAnsiConsole
{
    private readonly IAnsiConsole _underlying;
    private readonly ConcurrentQueue<Action> _queue;

    public BufferingAnsiConsole( IAnsiConsole underlying, ConcurrentQueue<Action> queue )
    {
        this._underlying = underlying;
        this._queue = queue;
    }

    public void Clear( bool home ) => this._queue.Enqueue( () => this._underlying.Clear( home ) );

    public void Write( IRenderable renderable ) => this._queue.Enqueue( () => this._underlying.Write( renderable ) );

    public Profile Profile => this._underlying.Profile;

    public IAnsiConsoleCursor Cursor => throw new NotSupportedException();

    public IAnsiConsoleInput Input => throw new NotSupportedException();

    public IExclusivityMode ExclusivityMode => throw new NotSupportedException();

    public RenderPipeline Pipeline => throw new NotSupportedException();
}