namespace PostSharp.Engineering.BuildTools.Build
{
    public enum BuildConfiguration
    {
        /// <summary>
        /// A debug build: non-optimized, non-obfuscated, including all PDBs, with an auto-generated version number.
        /// </summary>
        Debug,

        /// <summary>
        /// A release  build: optimized, obfuscated (if the solution supports it), with an auto-generated version number. 
        /// </summary>
        Release,

        /// <summary>
        /// Same as <see cref="Release"/>, but with the public version number.
        /// </summary>
        Public
    }
}