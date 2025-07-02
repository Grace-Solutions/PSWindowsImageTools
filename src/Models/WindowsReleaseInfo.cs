using System;
using System.Collections.Generic;
using System.Linq;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents Windows release information for a specific operating system and release ID
    /// </summary>
    public class WindowsReleaseInfo
    {
        /// <summary>
        /// Operating system name (Windows 10, Windows 11, Windows Server 2019, etc.)
        /// </summary>
        public string OperatingSystem { get; set; } = string.Empty;

        /// <summary>
        /// Type of operating system (Client or Server)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Release ID (21H2, 22H2, 23H2, 24H2, etc.)
        /// </summary>
        public string ReleaseID { get; set; } = string.Empty;

        /// <summary>
        /// Initial release version when this release was first made available
        /// </summary>
        public Version InitialReleaseVersion { get; set; } = new Version();

        /// <summary>
        /// Whether this release has a Long Term Servicing Channel (LTSC) or Long Term Servicing Branch (LTSB) build
        /// </summary>
        public bool HasLongTermServicingBuild { get; set; }

        /// <summary>
        /// Collection of all releases/updates for this operating system and release ID
        /// </summary>
        public WindowsRelease[] Releases { get; set; } = Array.Empty<WindowsRelease>();

        /// <summary>
        /// Gets the number of releases/updates available
        /// </summary>
        public int ReleaseCount => Releases?.Length ?? 0;

        /// <summary>
        /// Gets the latest release in this collection
        /// </summary>
        public WindowsRelease? LatestRelease => Releases?.OrderByDescending(r => r.AvailabilityDate).FirstOrDefault();
    }

    /// <summary>
    /// Represents a specific Windows release/update
    /// </summary>
    public class WindowsRelease
    {
        /// <summary>
        /// Servicing options for this release (LTSC, General Availability Channel, etc.)
        /// </summary>
        public string[] ServicingOptions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Date when this release became available
        /// </summary>
        public DateTime AvailabilityDate { get; set; }

        /// <summary>
        /// Version number for this release
        /// </summary>
        public Version Version { get; set; } = new Version();

        /// <summary>
        /// KB article number (if available)
        /// </summary>
        public string KBArticle { get; set; } = string.Empty;

        /// <summary>
        /// URL to the KB article
        /// </summary>
        public Uri? KBArticleURL { get; set; }

        /// <summary>
        /// Whether this is the latest release in the collection
        /// </summary>
        public bool IsLatest { get; set; }
    }
}
