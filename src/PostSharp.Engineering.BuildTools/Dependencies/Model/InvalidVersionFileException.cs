// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Runtime.Serialization;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model
{
    [Serializable]
    public class InvalidVersionFileException : Exception
    {
        public InvalidVersionFileException() { }

        public InvalidVersionFileException( string message ) : base( message ) { }

        public InvalidVersionFileException( string message, Exception inner ) : base( message, inner ) { }

        protected InvalidVersionFileException(
            SerializationInfo info,
            StreamingContext context ) : base( info, context ) { }
    }
}