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
        public string ReleaseId { get; set; } = string.Empty;

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
        /// Gets the latest release in this collection
        /// </summary>
        public WindowsRelease? LatestRelease => Releases?.OrderByDescending(r => r.AvailabilityDate).FirstOrDefault();

        /// <summary>
        /// Gets the latest KB article for this release
        /// </summary>
        public string LatestKBArticle => LatestRelease?.KBArticle ?? string.Empty;

        /// <summary>
        /// Gets the latest version for this release
        /// </summary>
        public Version LatestVersion => LatestRelease?.Version ?? InitialReleaseVersion;

        /// <summary>
        /// Gets the build number for this release
        /// </summary>
        public int BuildNumber => InitialReleaseVersion.Build;

        /// <summary>
        /// Gets the number of releases/updates available
        /// </summary>
        public int ReleaseCount => Releases?.Length ?? 0;

        /// <summary>
        /// Gets the normalized operating system name
        /// </summary>
        public string NormalizedOperatingSystem => Services.FormatUtilityService.NormalizeOperatingSystemName(OperatingSystem);

        /// <summary>
        /// Gets the normalized release ID
        /// </summary>
        public string NormalizedReleaseId => Services.FormatUtilityService.NormalizeReleaseId(ReleaseId);

        /// <summary>
        /// Gets the age of the latest release
        /// </summary>
        public TimeSpan LatestReleaseAge => LatestRelease != null ? DateTime.Now - LatestRelease.AvailabilityDate : TimeSpan.Zero;

        /// <summary>
        /// Gets the formatted age of the latest release
        /// </summary>
        public string LatestReleaseAgeFormatted => Services.FormatUtilityService.FormatDuration(LatestReleaseAge);

        /// <summary>
        /// Gets releases that have KB articles
        /// </summary>
        public WindowsRelease[] ReleasesWithKB => Releases?.Where(r => !string.IsNullOrEmpty(r.KBArticle)).ToArray() ?? Array.Empty<WindowsRelease>();

        /// <summary>
        /// Gets the number of releases with KB articles
        /// </summary>
        public int ReleasesWithKBCount => ReleasesWithKB.Length;

        /// <summary>
        /// Gets all KB articles for this release
        /// </summary>
        public string[] AllKBArticles => Releases?.Where(r => !string.IsNullOrEmpty(r.KBArticle))
                                                   .Select(r => r.KBArticle)
                                                   .Distinct()
                                                   .ToArray() ?? Array.Empty<string>();

        /// <summary>
        /// Gets all servicing options available for this release
        /// </summary>
        public string[] AllServicingOptions => Releases?.SelectMany(r => r.ServicingOptions)
                                                        .Distinct()
                                                        .ToArray() ?? Array.Empty<string>();

        /// <summary>
        /// Whether this release supports LTSC/LTSB servicing
        /// </summary>
        public bool SupportsLTSC => AllServicingOptions.Any(s =>
            s.IndexOf("LTSC", StringComparison.OrdinalIgnoreCase) >= 0 ||
            s.IndexOf("LTSB", StringComparison.OrdinalIgnoreCase) >= 0 ||
            s.IndexOf("Long Term", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>
        /// Returns a string representation of the release info
        /// </summary>
        public override string ToString()
        {
            return $"{OperatingSystem} {ReleaseId} (Build {BuildNumber}) - {ReleaseCount} releases";
        }
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
        public string KBArticleURL { get; set; } = string.Empty;

        /// <summary>
        /// Build number for this release
        /// </summary>
        public int BuildNumber => Version.Build;

        /// <summary>
        /// Revision number for this release
        /// </summary>
        public int RevisionNumber => Version.Revision;

        /// <summary>
        /// Whether this release has a KB article
        /// </summary>
        public bool HasKBArticle => !string.IsNullOrEmpty(KBArticle);

        /// <summary>
        /// Whether this release supports LTSC/LTSB
        /// </summary>
        public bool IsLTSC => ServicingOptions.Any(s =>
            s.IndexOf("LTSC", StringComparison.OrdinalIgnoreCase) >= 0 ||
            s.IndexOf("LTSB", StringComparison.OrdinalIgnoreCase) >= 0 ||
            s.IndexOf("Long Term", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>
        /// Formatted servicing options as a single string
        /// </summary>
        public string ServicingOptionsFormatted => string.Join(", ", ServicingOptions);

        /// <summary>
        /// Returns a string representation of the release
        /// </summary>
        public override string ToString()
        {
            var kbInfo = HasKBArticle ? $" ({KBArticle})" : "";
            return $"{Version}{kbInfo} - {AvailabilityDate:yyyy-MM-dd}";
        }
    }
}
