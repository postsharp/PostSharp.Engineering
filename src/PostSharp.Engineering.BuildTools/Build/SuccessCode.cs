// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

namespace PostSharp.Engineering.BuildTools.Build
{
    public enum SuccessCode
    {
        /// <summary>
        /// Success.
        /// </summary>
        Success,

        /// <summary>
        /// Error, but we can try to continue to the next item.
        /// </summary>
        Error,

        /// <summary>
        /// Error, and we have to stop immediately.
        /// </summary>
        Fatal
    }
}