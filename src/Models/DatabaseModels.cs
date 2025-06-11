using System;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Base class for database entities
    /// </summary>
    public abstract class DatabaseEntity
    {
        /// <summary>
        /// Unique identifier (UUIDv4)
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Creation timestamp (UTC)
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last modification timestamp (UTC)
        /// </summary>
        public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a build record in the database
    /// </summary>
    public class BuildRecord : DatabaseEntity
    {
        /// <summary>
        /// Source image path
        /// </summary>
        public string SourceImagePath { get; set; } = string.Empty;

        /// <summary>
        /// SHA256 hash of source image
        /// </summary>
        public string SourceImageHash { get; set; } = string.Empty;

        /// <summary>
        /// Output image path
        /// </summary>
        public string OutputImagePath { get; set; } = string.Empty;

        /// <summary>
        /// SHA256 hash of output image
        /// </summary>
        public string OutputImageHash { get; set; } = string.Empty;

        /// <summary>
        /// Recipe used for the build (JSON)
        /// </summary>
        public string RecipeJson { get; set; } = string.Empty;

        /// <summary>
        /// Build status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Error message if build failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Total processing duration in milliseconds
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Number of images processed
        /// </summary>
        public int ImageCount { get; set; }
    }

    /// <summary>
    /// Represents an update record from Windows Update Catalog
    /// </summary>
    public class UpdateRecord : DatabaseEntity
    {
        /// <summary>
        /// Update title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Update description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Download URL
        /// </summary>
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// Update size in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Last updated date
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Product information
        /// </summary>
        public string Product { get; set; } = string.Empty;

        /// <summary>
        /// Classification
        /// </summary>
        public string Classification { get; set; } = string.Empty;

        /// <summary>
        /// Supported products
        /// </summary>
        public string SupportedProducts { get; set; } = string.Empty;

        /// <summary>
        /// MSRC severity
        /// </summary>
        public string MsrcSeverity { get; set; } = string.Empty;

        /// <summary>
        /// KB article number
        /// </summary>
        public string KbArticleId { get; set; } = string.Empty;

        /// <summary>
        /// Search query that found this update
        /// </summary>
        public string SearchQuery { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a download record
    /// </summary>
    public class DownloadRecord : DatabaseEntity
    {
        /// <summary>
        /// Download URL
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Local file path
        /// </summary>
        public string LocalPath { get; set; } = string.Empty;

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// SHA256 hash of downloaded file
        /// </summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// Download status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Download duration in milliseconds
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Error message if download failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Related update record ID
        /// </summary>
        public string? UpdateRecordId { get; set; }
    }

    /// <summary>
    /// Represents a build processing event
    /// </summary>
    public class BuildProcessingEvent : DatabaseEntity
    {
        /// <summary>
        /// Related build record ID
        /// </summary>
        public string BuildRecordId { get; set; } = string.Empty;

        /// <summary>
        /// Event type (e.g., "ImageFilter", "RemoveAppxPackages", etc.)
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Event description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Event status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Event start time (UTC)
        /// </summary>
        public DateTime StartTimeUtc { get; set; }

        /// <summary>
        /// Event end time (UTC)
        /// </summary>
        public DateTime EndTimeUtc { get; set; }

        /// <summary>
        /// Event duration in milliseconds
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Error message if event failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Additional event data (JSON)
        /// </summary>
        public string EventData { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuration record for database settings
    /// </summary>
    public class ConfigurationRecord : DatabaseEntity
    {
        /// <summary>
        /// Configuration key
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Configuration value
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Configuration description
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}
