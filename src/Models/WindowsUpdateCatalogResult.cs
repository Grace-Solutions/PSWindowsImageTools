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
    }
}
