namespace PostSharp.Engineering.BuildTools.Build.Model
{
    /// <summary>
    /// Enumerates the method that determines what <see cref="BuildCommand"/> or <see cref="Product.Build"/>
    /// should do. This property is exposed by <see cref="Solution"/>.
    /// </summary>
    public enum BuildMethod
    {
        /// <summary>
        /// No action is executed.
        /// </summary>
        None,

        /// <summary>
        /// Building should call <see cref="Solution.Build"/>.
        /// </summary>
        Build,

        /// <summary>
        /// Building should call <see cref="Solution.Test"/>.
        /// </summary>
        Test,

        /// <summary>
        /// Building should call <see cref="Solution.Pack"/>.
        /// </summary>
        Pack
    }
}