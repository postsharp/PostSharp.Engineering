// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Spectre.Console;
using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace PostSharp.Engineering.BuildTools.Utilities
{
    public class ConsoleHelper
    {
        private static readonly CancellationTokenSource _cancellationTokenSource = new();

        static ConsoleHelper()
        {
            Console.CancelKeyPress += OnCancel;
        }

        private static void OnCancel( object? sender, ConsoleCancelEventArgs e ) => _cancellationTokenSource.Cancel();

        public static CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public IAnsiConsole Out { get; }

        public IAnsiConsole Error { get; }

        public void WriteError( string format, params object[] args ) => this.WriteError( string.Format( CultureInfo.InvariantCulture, format, args ) );

        public void WriteError( string message ) => this.Error.MarkupLine( $"[red]{message.EscapeMarkup()}[/]" );

        public void WriteWarning( string message ) => this.Out.MarkupLine( $"[yellow]{message.EscapeMarkup()}[/]" );

        public void WriteWarning( string format, params object[] args ) => this.WriteWarning( string.Format( CultureInfo.InvariantCulture, format, args ) );

        public void WriteMessage( string message ) => this.Out.MarkupLine( "[dim]" + message.EscapeMarkup() + "[/]" );

        public void WriteMessage( string format, params object[] args ) => this.WriteMessage( string.Format( CultureInfo.InvariantCulture, format, args ) );

        public void WriteImportantMessage( string message ) => this.Out.MarkupLine( "[bold]" + message.EscapeMarkup() + "[/]" );

        public void WriteImportantMessage( string format, params object[] args )
            => this.WriteImportantMessage( string.Format( CultureInfo.InvariantCulture, format, args ) );

        public void WriteSuccess( string message ) => this.Out.MarkupLine( $"[green]{message.EscapeMarkup()}[/]" );

        public void WriteHeading( string message )
            => this.Out.MarkupLine( $"[bold cyan]===== {message.EscapeMarkup()} {new string( '=', 160 - message.Length )}[/]" );

        public ConsoleHelper()
        {
            IAnsiConsole CreateConsole( TextWriter writer )
            {
                return AnsiConsole.Create( new AnsiConsoleSettings { Out = new AnsiConsoleOutputWrapper( writer ) } );
            }

            this.Out = CreateConsole( Console.Out );
            this.Error = CreateConsole( Console.Error );
        }
    }
}