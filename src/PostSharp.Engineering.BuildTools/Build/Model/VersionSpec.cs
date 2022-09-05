// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public readonly struct VersionSpec
    {
        public VersionKind Kind { get; }

        public int Number { get; }

        public VersionSpec( VersionKind kind, int number = 0 )
        {
            this.Kind = kind;
            this.Number = number;
        }
    }
}