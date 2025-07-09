namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Specifies the mount mode for Windows images
    /// </summary>
    public enum MountMode
    {
        /// <summary>
        /// Mount the image in read-only mode (default, faster, safer)
        /// </summary>
        ReadOnly = 0,

        /// <summary>
        /// Mount the image in read-write mode (allows modifications)
        /// </summary>
        ReadWrite = 1
    }
}
