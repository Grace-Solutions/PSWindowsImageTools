using System;
using System.IO;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents a downloaded Windows Update package ready for installation
    /// </summary>
    public class WindowsUpdatePackage
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
        /// Local file where the update package is stored
        /// </summary>
        public FileInfo LocalFile { get; set; } = null!;

        /// <summary>
        /// Whether the package has been successfully downloaded
        /// </summary>
        public bool IsDownloaded { get; set; }

        /// <summary>
        /// Whether the package file has been verified for integrity
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// SHA256 hash of the downloaded file
        /// </summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// When the package was downloaded
        /// </summary>
        public DateTime DownloadedAt { get; set; }

        /// <summary>
        /// Size of the downloaded file in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// The original catalog result this package was created from
        /// </summary>
        public WindowsUpdateCatalogResult SourceCatalogResult { get; set; } = new WindowsUpdateCatalogResult();

        /// <summary>
        /// Any error message if download or verification failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Download URL that was used
        /// </summary>
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// Returns a string representation of the package
        /// </summary>
        public override string ToString()
        {
            var status = IsDownloaded ? (IsVerified ? "Verified" : "Downloaded") : "Not Downloaded";
            return $"{KBNumber} - {Title} ({status})";
        }

        /// <summary>
        /// Gets a human-readable file size string
        /// </summary>
        public string FileSizeFormatted
        {
            get
            {
                if (FileSize == 0) return "Unknown";
                
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = FileSize;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }

        /// <summary>
        /// Gets the local file path as a string (for compatibility)
        /// </summary>
        public string LocalFilePath => LocalFile.FullName;
    }
}
