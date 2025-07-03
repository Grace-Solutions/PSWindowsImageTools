using System;
using System.Collections.Generic;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents an AppX provisioned package in a Windows image
    /// </summary>
    public class AppXProvisionedPackage
    {
        /// <summary>
        /// Full package name (e.g., "Microsoft.BingWeather_4.25.20211.0_neutral_~_8wekyb3d8bbwe")
        /// </summary>
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the package (e.g., "MSN Weather")
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Package family name (e.g., "Microsoft.BingWeather_8wekyb3d8bbwe")
        /// </summary>
        public string PackageFamilyName { get; set; } = string.Empty;

        /// <summary>
        /// Publisher name
        /// </summary>
        public string Publisher { get; set; } = string.Empty;

        /// <summary>
        /// Package version
        /// </summary>
        public Version? Version { get; set; }

        /// <summary>
        /// Package architecture (e.g., "neutral", "x64", "x86")
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// Resource ID
        /// </summary>
        public string ResourceId { get; set; } = string.Empty;

        /// <summary>
        /// Publisher ID
        /// </summary>
        public string PublisherId { get; set; } = string.Empty;

        /// <summary>
        /// Regions where this package is applicable
        /// </summary>
        public List<string> Regions { get; set; } = new List<string>();

        /// <summary>
        /// Whether this package matches inclusion filter
        /// </summary>
        public bool MatchesInclusionFilter { get; set; }

        /// <summary>
        /// Whether this package matches exclusion filter
        /// </summary>
        public bool MatchesExclusionFilter { get; set; }

        /// <summary>
        /// Whether this package should be removed (after applying filters)
        /// </summary>
        public bool ShouldBeRemoved => MatchesInclusionFilter && !MatchesExclusionFilter;

        /// <summary>
        /// Returns a string representation of the package
        /// </summary>
        public override string ToString()
        {
            return $"{DisplayName} ({PackageName})";
        }
    }

    /// <summary>
    /// Represents the result of removing AppX provisioned packages from a mounted Windows image
    /// </summary>
    public class AppXRemovalResult
    {
        /// <summary>
        /// The mounted image that packages were removed from
        /// </summary>
        public MountedWindowsImage MountedImage { get; set; } = new MountedWindowsImage();

        /// <summary>
        /// Total number of provisioned packages found in the image
        /// </summary>
        public int TotalPackagesFound { get; set; }

        /// <summary>
        /// Number of packages that matched the inclusion filter
        /// </summary>
        public int PackagesMatchingInclusion { get; set; }

        /// <summary>
        /// Number of packages that matched the exclusion filter
        /// </summary>
        public int PackagesMatchingExclusion { get; set; }

        /// <summary>
        /// Number of packages targeted for removal (after applying filters)
        /// </summary>
        public int PackagesTargetedForRemoval { get; set; }

        /// <summary>
        /// List of packages that were successfully removed
        /// </summary>
        public List<AppXProvisionedPackage> SuccessfullyRemovedPackages { get; set; } = new List<AppXProvisionedPackage>();

        /// <summary>
        /// List of packages that failed to be removed
        /// </summary>
        public List<AppXProvisionedPackage> FailedToRemovePackages { get; set; } = new List<AppXProvisionedPackage>();

        /// <summary>
        /// Error messages for failed removals
        /// </summary>
        public Dictionary<string, string> ErrorMessages { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Number of successful removals
        /// </summary>
        public int SuccessCount => SuccessfullyRemovedPackages.Count;

        /// <summary>
        /// Number of failed removals
        /// </summary>
        public int FailureCount => FailedToRemovePackages.Count;

        /// <summary>
        /// Success percentage
        /// </summary>
        public double SuccessPercentage => PackagesTargetedForRemoval > 0 ? (double)SuccessCount / PackagesTargetedForRemoval * 100 : 0;

        /// <summary>
        /// Whether all targeted packages were removed successfully
        /// </summary>
        public bool IsCompletelySuccessful => FailureCount == 0 && SuccessCount > 0;

        /// <summary>
        /// Returns a string representation of the removal result
        /// </summary>
        public override string ToString()
        {
            return $"{MountedImage.ImageName}: {SuccessCount} removed, {FailureCount} failed ({SuccessPercentage:F1}%)";
        }
    }
}
