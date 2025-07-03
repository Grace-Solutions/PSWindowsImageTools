using System;
using System.Collections.Generic;
using System.IO;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents an INF driver file with optional parsed metadata
    /// </summary>
    public class INFDriverInfo
    {
        /// <summary>
        /// FileInfo object for the INF file
        /// </summary>
        public FileInfo INFFile { get; set; } = null!;

        /// <summary>
        /// Parsed INF metadata (null if not parsed)
        /// </summary>
        public INFDriverParseResult? ParsedInfo { get; set; }

        /// <summary>
        /// Directory containing the INF file and associated driver files
        /// </summary>
        public DirectoryInfo? DriverDirectory => INFFile.Directory;

        /// <summary>
        /// Returns a string representation of the driver info
        /// </summary>
        public override string ToString()
        {
            if (ParsedInfo != null)
            {
                return $"{ParsedInfo.DriverName} ({ParsedInfo.Version?.ToString() ?? "Unknown"}) - {INFFile.Name}";
            }

            var directoryName = DriverDirectory?.Name ?? "Unknown Directory";
            return $"{INFFile.Name} - {directoryName}";
        }
    }

    /// <summary>
    /// Represents parsed metadata from an INF driver file
    /// </summary>
    public class INFDriverParseResult
    {
        /// <summary>
        /// Driver name from the INF file
        /// </summary>
        public string DriverName { get; set; } = string.Empty;

        /// <summary>
        /// Driver version
        /// </summary>
        public Version? Version { get; set; }

        /// <summary>
        /// Driver date
        /// </summary>
        public DateTime? DriverDate { get; set; }

        /// <summary>
        /// Provider name (manufacturer)
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Driver class (e.g., "Net", "Display", "System")
        /// </summary>
        public string Class { get; set; } = string.Empty;

        /// <summary>
        /// Driver class GUID
        /// </summary>
        public string ClassGuid { get; set; } = string.Empty;

        /// <summary>
        /// Catalog file as FileInfo object (null if not specified)
        /// </summary>
        public FileInfo? CatalogFile { get; set; }

        /// <summary>
        /// Supported architectures
        /// </summary>
        public List<string> SupportedArchitectures { get; set; } = new List<string>();

        /// <summary>
        /// Hardware IDs supported by this driver
        /// </summary>
        public List<string> HardwareIds { get; set; } = new List<string>();

        /// <summary>
        /// Compatible IDs supported by this driver
        /// </summary>
        public List<string> CompatibleIds { get; set; } = new List<string>();

        /// <summary>
        /// Whether the driver is digitally signed
        /// </summary>
        public bool IsSigned { get; set; }

        /// <summary>
        /// Any parsing errors encountered
        /// </summary>
        public List<string> ParseErrors { get; set; } = new List<string>();

        /// <summary>
        /// Additional properties found in the INF file
        /// </summary>
        public Dictionary<string, string> AdditionalProperties { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Returns a string representation of the parsed driver info
        /// </summary>
        public override string ToString()
        {
            return $"{DriverName} v{Version?.ToString() ?? "Unknown"} ({Class}) by {Provider}";
        }
    }

    /// <summary>
    /// Represents the result of adding drivers to a mounted Windows image
    /// </summary>
    public class DriverInstallationResult
    {
        /// <summary>
        /// The mounted image that drivers were added to
        /// </summary>
        public MountedWindowsImage MountedImage { get; set; } = new MountedWindowsImage();

        /// <summary>
        /// List of drivers that were successfully installed
        /// </summary>
        public List<INFDriverInfo> SuccessfulDrivers { get; set; } = new List<INFDriverInfo>();

        /// <summary>
        /// List of drivers that failed to install
        /// </summary>
        public List<INFDriverInfo> FailedDrivers { get; set; } = new List<INFDriverInfo>();

        /// <summary>
        /// Error messages for failed installations
        /// </summary>
        public Dictionary<string, string> ErrorMessages { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Total number of drivers processed
        /// </summary>
        public int TotalDrivers => SuccessfulDrivers.Count + FailedDrivers.Count;

        /// <summary>
        /// Number of successful installations
        /// </summary>
        public int SuccessCount => SuccessfulDrivers.Count;

        /// <summary>
        /// Number of failed installations
        /// </summary>
        public int FailureCount => FailedDrivers.Count;

        /// <summary>
        /// Success percentage
        /// </summary>
        public double SuccessPercentage => TotalDrivers > 0 ? (double)SuccessCount / TotalDrivers * 100 : 0;

        /// <summary>
        /// Whether all drivers were installed successfully
        /// </summary>
        public bool IsCompletelySuccessful => FailureCount == 0 && SuccessCount > 0;

        /// <summary>
        /// Returns a string representation of the installation result
        /// </summary>
        public override string ToString()
        {
            return $"{MountedImage.ImageName}: {SuccessCount} successful, {FailureCount} failed ({SuccessPercentage:F1}%)";
        }
    }
}
