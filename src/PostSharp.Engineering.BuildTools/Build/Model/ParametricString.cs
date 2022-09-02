// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    /// <summary>
    /// A string that can contain the following parameters: <c>$(PackageVersion)</c>, <c>$(Configuration)</c> or <c>$(MSSBuildConfiguration)</c>.
    /// </summary>
    public readonly struct ParametricString
    {
        private readonly string? _value;

        private ParametricString( string value )
        {
            this._value = value;
        }

        public override string ToString() => this._value ?? "<null>";

        public string ToString( BuildInfo parameters )
            => this._value?
                .Replace( "$(PackageVersion)", parameters.PackageVersion, StringComparison.OrdinalIgnoreCase )
                .Replace( "$(Configuration)", parameters.Configuration, StringComparison.OrdinalIgnoreCase )
                .Replace( "$(MSSBuildConfiguration)", parameters.MSBuildConfiguration, StringComparison.OrdinalIgnoreCase ) ?? "";

        public static implicit operator ParametricString( string value ) => new( value );
    }
}