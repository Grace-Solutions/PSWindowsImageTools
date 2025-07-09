using System;
using System.IO;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Configuration settings for wallpaper and lockscreen setup
    /// </summary>
    public class WallpaperConfiguration
    {
        /// <summary>
        /// Source wallpaper image file
        /// </summary>
        public FileInfo WallpaperSourcePath { get; set; }

        /// <summary>
        /// Source lockscreen image file (optional)
        /// </summary>
        public FileInfo? LockscreenSourcePath { get; set; }

        /// <summary>
        /// Mount path or Windows directory root
        /// </summary>
        public DirectoryInfo MountPath { get; set; }

        /// <summary>
        /// Temporary directory for image processing
        /// </summary>
        public DirectoryInfo ImageScratchDirectory { get; set; }

        /// <summary>
        /// Target lockscreen destination path
        /// </summary>
        public FileInfo LockscreenDestinationPath { get; set; }

        /// <summary>
        /// Primary wallpaper destination path
        /// </summary>
        public FileInfo WallpaperDestinationPath { get; set; }

        /// <summary>
        /// Directory for default wallpapers (multiple resolutions)
        /// </summary>
        public DirectoryInfo DefaultWallpapersDestinationPath { get; set; }

        /// <summary>
        /// Resolution list for wallpaper generation
        /// </summary>
        public ResolutionInfo[] ResolutionList { get; set; }

        /// <summary>
        /// Creates a new WallpaperConfiguration
        /// </summary>
        /// <param name="mountPath">Mount path or Windows directory</param>
        /// <param name="wallpaperSource">Source wallpaper image</param>
        /// <param name="lockscreenSource">Source lockscreen image (optional)</param>
        /// <param name="resolutionList">Resolution list (uses default if null)</param>
        public WallpaperConfiguration(DirectoryInfo mountPath, FileInfo wallpaperSource, FileInfo? lockscreenSource = null, ResolutionInfo[]? resolutionList = null)
        {
            MountPath = mountPath ?? throw new ArgumentNullException(nameof(mountPath));
            WallpaperSourcePath = wallpaperSource ?? throw new ArgumentNullException(nameof(wallpaperSource));
            LockscreenSourcePath = lockscreenSource;
            ResolutionList = resolutionList ?? ResolutionInfo.GetDefaultResolutions();

            // Set up paths based on mount directory
            var tempPath = Path.GetTempPath();
            ImageScratchDirectory = new DirectoryInfo(Path.Combine(tempPath, "PSWindowsImageTools", "Images"));

            // Target paths within the mounted Windows image
            LockscreenDestinationPath = new FileInfo(Path.Combine(mountPath.FullName, "Windows", "Web", "Screen", "img100.jpg"));
            WallpaperDestinationPath = new FileInfo(Path.Combine(mountPath.FullName, "Windows", "Web", "Wallpaper", "Windows", "img0.jpg"));
            DefaultWallpapersDestinationPath = new DirectoryInfo(Path.Combine(mountPath.FullName, "Windows", "Web", "4K", "Wallpaper", "Windows"));
        }

        /// <summary>
        /// Validates the configuration
        /// </summary>
        public void Validate()
        {
            if (!WallpaperSourcePath.Exists)
                throw new FileNotFoundException($"Wallpaper source file not found: {WallpaperSourcePath.FullName}");

            if (LockscreenSourcePath != null && !LockscreenSourcePath.Exists)
                throw new FileNotFoundException($"Lockscreen source file not found: {LockscreenSourcePath.FullName}");

            if (!MountPath.Exists)
                throw new DirectoryNotFoundException($"Mount path not found: {MountPath.FullName}");

            // Validate Windows directory structure exists
            var windowsDir = new DirectoryInfo(Path.Combine(MountPath.FullName, "Windows"));
            if (!windowsDir.Exists)
                throw new DirectoryNotFoundException($"Windows directory not found in mount path: {windowsDir.FullName}");
        }
    }
}
