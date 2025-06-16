using System;
using System.Collections.Generic;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents a Windows Update from the Microsoft Update Catalog
    /// Simplified for pipeline operations
    /// </summary>
    public class WindowsUpdate
    {
        /// <summary>
        /// Unique identifier for the update
        /// </summary>
        public string UpdateId { get; set; } = string.Empty;

        /// <summary>
        /// Title of the update
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// KB article number (if applicable)
        /// </summary>
        public string KBNumber { get; set; } = string.Empty;

        /// <summary>
        /// Products that this update applies to
        /// </summary>
        public string Products { get; set; } = string.Empty;

        /// <summary>
        /// Classification of the update (Security Updates, Critical Updates, etc.)
        /// </summary>
        public string Classification { get; set; } = string.Empty;

        /// <summary>
        /// Architecture (x86, x64, ARM64, etc.)
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// Version of the update
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Date when the update was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Size of the update in bytes
        /// </summary>
        public long SizeInBytes { get; set; }

        /// <summary>
        /// Human-readable size string
        /// </summary>
        public string SizeFormatted { get; set; } = string.Empty;

        /// <summary>
        /// Download URLs for the update
        /// </summary>
        public List<string> DownloadUrls { get; set; } = new List<string>();

        /// <summary>
        /// Whether this is a superseded update
        /// </summary>
        public bool IsSuperseded { get; set; }

        /// <summary>
        /// Whether this update requires a restart
        /// </summary>
        public bool RequiresRestart { get; set; }

        /// <summary>
        /// Local file path if downloaded
        /// </summary>
        public string LocalFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Whether the update has been downloaded
        /// </summary>
        public bool IsDownloaded => !string.IsNullOrEmpty(LocalFilePath) && System.IO.File.Exists(LocalFilePath);

        /// <summary>
        /// When this record was added to the database
        /// </summary>
        public DateTime DatabaseTimestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Hash of the update content for change detection
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// Returns a string representation of the update
        /// </summary>
        public override string ToString()
        {
            return $"{KBNumber} - {Title} ({Architecture})";
        }
    }

    /// <summary>
    /// Search criteria for Windows Update catalog
    /// </summary>
    public class WindowsUpdateSearchCriteria
    {
        /// <summary>
        /// Search query string
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Product filter (Windows 11, Windows 10, etc.)
        /// </summary>
        public string? Product { get; set; }

        /// <summary>
        /// Classification filter (Security Updates, Critical Updates, etc.)
        /// </summary>
        public string? Classification { get; set; }

        /// <summary>
        /// Architecture filter (x64, x86, ARM64)
        /// </summary>
        public string? Architecture { get; set; }

        /// <summary>
        /// Language filter
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Date range start
        /// </summary>
        public DateTime? DateFrom { get; set; }

        /// <summary>
        /// Date range end
        /// </summary>
        public DateTime? DateTo { get; set; }

        /// <summary>
        /// Maximum number of results to return
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// Page number for pagination
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Number of results per page
        /// </summary>
        public int PageSize { get; set; } = 25;

        /// <summary>
        /// Sort field
        /// </summary>
        public string SortBy { get; set; } = "LastUpdated";

        /// <summary>
        /// Sort direction (Ascending, Descending)
        /// </summary>
        public string SortDirection { get; set; } = "Descending";

        /// <summary>
        /// Include superseded updates
        /// </summary>
        public bool IncludeSuperseded { get; set; } = false;
    }

    /// <summary>
    /// Result of a Windows Update search operation
    /// </summary>
    public class WindowsUpdateSearchResult
    {
        /// <summary>
        /// Search criteria used
        /// </summary>
        public WindowsUpdateSearchCriteria Criteria { get; set; } = new WindowsUpdateSearchCriteria();

        /// <summary>
        /// Updates found
        /// </summary>
        public List<WindowsUpdate> Updates { get; set; } = new List<WindowsUpdate>();

        /// <summary>
        /// Total number of updates available (across all pages)
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int CurrentPage { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Whether there are more pages available
        /// </summary>
        public bool HasMorePages => CurrentPage < TotalPages;

        /// <summary>
        /// Search execution time
        /// </summary>
        public TimeSpan SearchDuration { get; set; }

        /// <summary>
        /// Whether the search was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if search failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of an update installation operation
    /// </summary>
    public class UpdateInstallationResult
    {
        /// <summary>
        /// Update that was installed
        /// </summary>
        public WindowsUpdate Update { get; set; } = new WindowsUpdate();

        /// <summary>
        /// Target image path
        /// </summary>
        public string ImagePath { get; set; } = string.Empty;

        /// <summary>
        /// Image index (if applicable)
        /// </summary>
        public int? ImageIndex { get; set; }

        /// <summary>
        /// Whether the installation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if installation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Installation start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Installation end time
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Installation duration
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// Whether a restart is required
        /// </summary>
        public bool RestartRequired { get; set; }

        /// <summary>
        /// Exit code from the installation
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Installation log output
        /// </summary>
        public string? LogOutput { get; set; }
    }
}
