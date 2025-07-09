using System;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents image resolution information for wallpaper resizing
    /// </summary>
    public class ResolutionInfo
    {
        /// <summary>
        /// Image name prefix (e.g., "img0_", "img19_")
        /// </summary>
        public string ImageName { get; set; } = string.Empty;

        /// <summary>
        /// Target width in pixels
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Target height in pixels
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Creates a new ResolutionInfo instance
        /// </summary>
        /// <param name="imageName">Image name prefix</param>
        /// <param name="width">Width in pixels</param>
        /// <param name="height">Height in pixels</param>
        public ResolutionInfo(string imageName, int width, int height)
        {
            ImageName = imageName ?? throw new ArgumentNullException(nameof(imageName));
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public ResolutionInfo() { }

        /// <summary>
        /// Returns a string representation of the resolution
        /// </summary>
        public override string ToString()
        {
            return $"{ImageName}{Width}x{Height}";
        }

        /// <summary>
        /// Gets the default Windows wallpaper resolution list
        /// </summary>
        public static ResolutionInfo[] GetDefaultResolutions()
        {
            return new[]
            {
                new ResolutionInfo("img0_", 768, 1024),
                new ResolutionInfo("img0_", 768, 1366),
                new ResolutionInfo("img0_", 1024, 768),
                new ResolutionInfo("img0_", 1200, 1920),
                new ResolutionInfo("img0_", 1366, 768),
                new ResolutionInfo("img0_", 1600, 2560),
                new ResolutionInfo("img0_", 2160, 3840),
                new ResolutionInfo("img0_", 1920, 1200),
                new ResolutionInfo("img19_", 1920, 1200),
                new ResolutionInfo("img0_", 2560, 1600),
                new ResolutionInfo("img0_", 3840, 2160)
            };
        }
    }
}
