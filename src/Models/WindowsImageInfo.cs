using System;
using System.Collections.Generic;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents detailed information about a Windows image
    /// </summary>
    public class WindowsImageInfo
    {
        /// <summary>
        /// Unique identifier for this image record
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Index number within the image file
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Display name of the image
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the image
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Windows edition (e.g., Pro, Enterprise, Home)
        /// </summary>
        public string Edition { get; set; } = string.Empty;

        /// <summary>
        /// Architecture (x86, x64, arm64)
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// Version information
        /// </summary>
        public Version? Version { get; set; }

        /// <summary>
        /// Build number
        /// </summary>
        public string Build { get; set; } = string.Empty;

        /// <summary>
        /// Service pack level
        /// </summary>
        public string ServicePackLevel { get; set; } = string.Empty;

        /// <summary>
        /// Full 4-point Windows version combining DISM version with UBR from registry
        /// Returns the complete version (e.g., 10.0.22631.2428) when advanced info is available,
        /// otherwise returns the DISM version (e.g., 10.0.22631.0)
        /// </summary>
        public Version? FullVersion
        {
            get
            {
                // If we have advanced registry info with UBR, combine it with the DISM version
                if (AdvancedInfo?.RegistryInfo?.TryGetValue("UBR", out var ubrValue) == true &&
                    Version != null &&
                    ubrValue != null)
                {
                    var ubrString = ubrValue.ToString();
                    if (!string.IsNullOrEmpty(ubrString) && int.TryParse(ubrString, out var ubr))
                    {
                        // Combine DISM version with UBR to create full 4-point version
                        return new Version(Version.Major, Version.Minor, Version.Build, ubr);
                    }
                }

                // Fall back to the DISM version if UBR is not available
                return Version;
            }
        }

        /// <summary>
        /// Installation type (Client, Server, etc.)
        /// </summary>
        public string InstallationType { get; set; } = string.Empty;

        /// <summary>
        /// Product type
        /// </summary>
        public string ProductType { get; set; } = string.Empty;

        /// <summary>
        /// Product suite
        /// </summary>
        public string ProductSuite { get; set; } = string.Empty;

        /// <summary>
        /// System root path
        /// </summary>
        public string SystemRoot { get; set; } = string.Empty;

        /// <summary>
        /// Default language
        /// </summary>
        public string DefaultLanguage { get; set; } = string.Empty;

        /// <summary>
        /// Available languages
        /// </summary>
        public List<string> Languages { get; set; } = new List<string>();

        /// <summary>
        /// Image size in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Creation timestamp (UTC)
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Last modification timestamp (UTC)
        /// </summary>
        public DateTime ModifiedTime { get; set; }

        /// <summary>
        /// Source image file path
        /// </summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// SHA256 hash of the source file
        /// </summary>
        public string SourceHash { get; set; } = string.Empty;

        /// <summary>
        /// Advanced metadata (populated when Advanced flag is used)
        /// </summary>
        public WindowsImageAdvancedInfo? AdvancedInfo { get; set; }

        /// <summary>
        /// Timestamp when this record was created (UTC)
        /// </summary>
        public DateTime RecordCreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when this record was last modified (UTC)
        /// </summary>
        public DateTime RecordModifiedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Advanced metadata obtained by mounting and reading registry
    /// </summary>
    public class WindowsImageAdvancedInfo
    {
        /// <summary>
        /// Installed Windows features
        /// </summary>
        public List<string> InstalledFeatures { get; set; } = new List<string>();

        /// <summary>
        /// Available Windows features
        /// </summary>
        public List<string> AvailableFeatures { get; set; } = new List<string>();

        /// <summary>
        /// Installed packages
        /// </summary>
        public List<string> InstalledPackages { get; set; } = new List<string>();

        /// <summary>
        /// Registry information
        /// </summary>
        public Dictionary<string, object> RegistryInfo { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Installed drivers
        /// </summary>
        public List<string> InstalledDrivers { get; set; } = new List<string>();

        /// <summary>
        /// Update information
        /// </summary>
        public List<string> InstalledUpdates { get; set; } = new List<string>();
    }
}
