using System;
using System.IO;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents a Windows Update search result from the Microsoft Update Catalog
    /// </summary>
    public class WindowsUpdateCatalogResult
    {
        /// <summary>
        /// Unique identifier for the update
        /// </summary>
        public string UpdateId { get; set; } = string.Empty;

        /// <summary>
        /// Knowledge Base article number (e.g., "KB5000001")
        /// </summary>
        public string KBNumber { get; set; } = string.Empty;

        /// <summary>
        /// Title of the update
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of the update
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Products this update applies to (e.g., "Windows 11", "Windows Server 2022")
        /// </summary>
        public string[] Products { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Classification of the update (e.g., "Security Updates", "Critical Updates")
        /// </summary>
        public string Classification { get; set; } = string.Empty;

        /// <summary>
        /// When the update was last modified
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Size of the update package in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Download URLs for the update package
        /// </summary>
        public string[] DownloadUrls { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Target architecture (e.g., "AMD64", "x86", "ARM64")
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// Supported languages
        /// </summary>
        public string[] Languages { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Whether download URLs have been retrieved
        /// </summary>
        public bool HasDownloadUrls { get; set; }

        /// <summary>
        /// Additional metadata from the catalog
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Returns a string representation of the catalog result
        /// </summary>
        public override string ToString()
        {
            return $"{KBNumber} - {Title} ({Architecture})";
        }

        /// <summary>
        /// Gets a human-readable size string
        /// </summary>
        public string SizeFormatted
        {
            get
            {
                if (Size == 0) return "Unknown";

                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = Size;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }

        #region Size Conversion Methods

        /// <summary>
        /// Converts the size from bytes to kilobytes
        /// </summary>
        /// <returns>Size in kilobytes (KB) rounded to 2 decimal places</returns>
        public double ToKB()
        {
            return Math.Round(Size / 1024.0, 2);
        }

        /// <summary>
        /// Converts the size from bytes to megabytes
        /// </summary>
        /// <returns>Size in megabytes (MB) rounded to 2 decimal places</returns>
        public double ToMB()
        {
            return Math.Round(Size / (1024.0 * 1024.0), 2);
        }

        /// <summary>
        /// Converts the size from bytes to gigabytes
        /// </summary>
        /// <returns>Size in gigabytes (GB) rounded to 2 decimal places</returns>
        public double ToGB()
        {
            return Math.Round(Size / (1024.0 * 1024.0 * 1024.0), 2);
        }

        /// <summary>
        /// Converts the size from bytes to terabytes
        /// </summary>
        /// <returns>Size in terabytes (TB) rounded to 2 decimal places</returns>
        public double ToTB()
        {
            return Math.Round(Size / (1024.0 * 1024.0 * 1024.0 * 1024.0), 2);
        }

        /// <summary>
        /// Returns a human-readable size string with appropriate unit
        /// </summary>
        /// <param name="decimals">Number of decimal places to show (default: 2)</param>
        /// <returns>Formatted size string (e.g., "1.23 MB")</returns>
        public string ToHumanReadableSize(int decimals = 2)
        {
            if (Size >= 1024L * 1024L * 1024L * 1024L) // TB
                return $"{ToTB().ToString($"F{decimals}")} TB";
            else if (Size >= 1024L * 1024L * 1024L) // GB
                return $"{ToGB().ToString($"F{decimals}")} GB";
            else if (Size >= 1024L * 1024L) // MB
                return $"{ToMB().ToString($"F{decimals}")} MB";
            else if (Size >= 1024L) // KB
                return $"{ToKB().ToString($"F{decimals}")} KB";
            else
                return $"{Size} bytes";
        }

        #endregion
    }
}
