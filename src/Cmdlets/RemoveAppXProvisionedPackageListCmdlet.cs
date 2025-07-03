using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using Microsoft.Dism;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Removes AppX provisioned packages from mounted Windows images with regex filtering
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "AppXProvisionedPackageList")]
    [OutputType(typeof(AppXRemovalResult[]))]
    public class RemoveAppXProvisionedPackageListCmdlet : PSCmdlet
    {
        private const string ComponentName = "Remove-AppXProvisionedPackageList";
        private readonly List<MountedWindowsImage> _allMountedImages = new List<MountedWindowsImage>();
        private Regex? _compiledInclusionFilter;
        private Regex? _compiledExclusionFilter;

        /// <summary>
        /// Mounted Windows images to remove AppX packages from
        /// </summary>
        [Parameter(
            Mandatory = true,
            Position = 0,
            ValueFromPipeline = true,
            HelpMessage = "Mounted Windows images to remove AppX provisioned packages from")]
        [ValidateNotNull]
        public MountedWindowsImage[] MountedImages { get; set; } = Array.Empty<MountedWindowsImage>();

        /// <summary>
        /// Regex pattern for inclusion filter on DisplayName property
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Regex pattern to include packages based on DisplayName (e.g., 'Microsoft.*' to include all Microsoft packages)")]
        [ValidateNotNullOrEmpty]
        public string? InclusionFilter { get; set; }

        /// <summary>
        /// Regex pattern for exclusion filter on DisplayName property
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Regex pattern to exclude packages based on DisplayName (e.g., 'Store|Calculator' to exclude Store and Calculator)")]
        [ValidateNotNullOrEmpty]
        public string? ExclusionFilter { get; set; }

        /// <summary>
        /// Processes pipeline input
        /// </summary>
        protected override void ProcessRecord()
        {
            _allMountedImages.AddRange(MountedImages);
        }

        /// <summary>
        /// Performs the AppX package removal operation
        /// </summary>
        protected override void EndProcessing()
        {
            if (_allMountedImages.Count == 0)
            {
                LoggingService.WriteWarning(this, "No mounted images provided for AppX package removal");
                return;
            }

            // Compile regex patterns with IgnoreCase and Multiline options
            try
            {
                if (!string.IsNullOrEmpty(InclusionFilter))
                {
                    _compiledInclusionFilter = new Regex(InclusionFilter, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                }

                if (!string.IsNullOrEmpty(ExclusionFilter))
                {
                    _compiledExclusionFilter = new Regex(ExclusionFilter, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, $"Invalid regex pattern: {ex.Message}", ex);
                throw;
            }

            var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName, "Remove AppX Provisioned Packages",
                $"Processing {_allMountedImages.Count} mounted images with filters - Inclusion: '{InclusionFilter ?? "None"}', Exclusion: '{ExclusionFilter ?? "None"}'");

            var results = new List<AppXRemovalResult>();

            try
            {
                // Process each mounted image
                for (int i = 0; i < _allMountedImages.Count; i++)
                {
                    var mountedImage = _allMountedImages[i];
                    var imageProgress = (int)((double)(i + 1) / _allMountedImages.Count * 100);

                    LoggingService.WriteProgress(this, "Removing AppX Provisioned Packages",
                        $"[{i + 1} of {_allMountedImages.Count}] - {mountedImage.ImageName}",
                        $"Processing {mountedImage.MountPath.FullName} ({imageProgress}%)", imageProgress);

                    try
                    {
                        var result = ProcessSingleImage(mountedImage, i + 1, _allMountedImages.Count);
                        results.Add(result);

                        LoggingService.WriteVerbose(this,
                            $"[{i + 1} of {_allMountedImages.Count}] - Completed AppX removal on {mountedImage.ImageName}: " +
                            $"{result.SuccessCount} removed, {result.FailureCount} failed");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteError(this, ComponentName,
                            $"[{i + 1} of {_allMountedImages.Count}] - Failed to process AppX packages on {mountedImage.ImageName}: {ex.Message}", ex);

                        // Create a failed result
                        var failedResult = new AppXRemovalResult
                        {
                            MountedImage = mountedImage
                        };
                        failedResult.ErrorMessages["General"] = ex.Message;
                        results.Add(failedResult);
                    }
                }

                // Output results
                foreach (var result in results)
                {
                    WriteObject(result);
                }

                // Summary
                var totalFound = results.Sum(r => r.TotalPackagesFound);
                var totalTargeted = results.Sum(r => r.PackagesTargetedForRemoval);
                var totalRemoved = results.Sum(r => r.SuccessCount);
                var totalFailed = results.Sum(r => r.FailureCount);

                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Remove AppX Provisioned Packages", operationStartTime,
                    $"Found {totalFound} packages, targeted {totalTargeted} for removal: {totalRemoved} removed, {totalFailed} failed");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, $"Failed to remove AppX provisioned packages: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Processes AppX package removal for a single mounted image
        /// </summary>
        private AppXRemovalResult ProcessSingleImage(MountedWindowsImage mountedImage, int currentImageIndex, int totalImages)
        {
            var result = new AppXRemovalResult
            {
                MountedImage = mountedImage
            };

            LoggingService.WriteVerbose(this,
                $"[{currentImageIndex} of {totalImages}] - Processing AppX packages on {mountedImage.ImageName}");

            try
            {
                using (var session = DismApi.OpenOfflineSession(mountedImage.MountPath.FullName))
                {
                    // Get all provisioned AppX packages
                    var provisionedPackages = GetProvisionedAppXPackages(session, currentImageIndex, totalImages);
                    result.TotalPackagesFound = provisionedPackages.Count;

                    LoggingService.WriteVerbose(this,
                        $"[{currentImageIndex} of {totalImages}] - Found {provisionedPackages.Count} provisioned AppX packages");

                    // Apply filters
                    ApplyFilters(provisionedPackages, result);

                    var packagesToRemove = provisionedPackages.Where(p => p.ShouldBeRemoved).ToList();
                    result.PackagesTargetedForRemoval = packagesToRemove.Count;

                    LoggingService.WriteVerbose(this,
                        $"[{currentImageIndex} of {totalImages}] - {packagesToRemove.Count} packages targeted for removal after filtering");

                    // Remove packages
                    if (packagesToRemove.Count > 0)
                    {
                        RemovePackages(session, packagesToRemove, result, currentImageIndex, totalImages);
                    }
                    else
                    {
                        LoggingService.WriteVerbose(this,
                            $"[{currentImageIndex} of {totalImages}] - No packages to remove after applying filters");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName,
                    $"[{currentImageIndex} of {totalImages}] - Failed to process {mountedImage.MountPath.FullName}: {ex.Message}", ex);
                result.ErrorMessages["Session"] = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Gets all provisioned AppX packages from the mounted image
        /// </summary>
        private List<AppXProvisionedPackage> GetProvisionedAppXPackages(DismSession session, int currentImageIndex, int totalImages)
        {
            var packages = new List<AppXProvisionedPackage>();

            try
            {
                LoggingService.WriteVerbose(this,
                    $"[{currentImageIndex} of {totalImages}] - Retrieving provisioned AppX packages using DISM API");

                var dismPackages = DismApi.GetProvisionedAppxPackages(session);

                foreach (var dismPackage in dismPackages)
                {
                    var package = new AppXProvisionedPackage
                    {
                        PackageName = dismPackage.PackageName ?? string.Empty,
                        DisplayName = dismPackage.DisplayName ?? string.Empty
                    };

                    // Try to set additional properties if they exist
                    try
                    {
                        // Use reflection to safely access properties that may not exist
                        var packageType = dismPackage.GetType();

                        var familyNameProp = packageType.GetProperty("PackageFamilyName");
                        if (familyNameProp != null)
                        {
                            package.PackageFamilyName = familyNameProp.GetValue(dismPackage)?.ToString() ?? string.Empty;
                        }

                        var publisherProp = packageType.GetProperty("Publisher");
                        if (publisherProp != null)
                        {
                            package.Publisher = publisherProp.GetValue(dismPackage)?.ToString() ?? string.Empty;
                        }

                        var versionProp = packageType.GetProperty("Version");
                        if (versionProp != null)
                        {
                            var versionStr = versionProp.GetValue(dismPackage)?.ToString();
                            if (!string.IsNullOrEmpty(versionStr))
                            {
                                package.Version = FormatUtilityService.ParseVersion(versionStr!);
                            }
                        }

                        var architectureProp = packageType.GetProperty("Architecture");
                        if (architectureProp != null)
                        {
                            package.Architecture = architectureProp.GetValue(dismPackage)?.ToString() ?? string.Empty;
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteVerbose(this, $"Could not access additional properties for package {package.PackageName}: {ex.Message}");
                    }

                    packages.Add(package);
                }

                LoggingService.WriteVerbose(this,
                    $"[{currentImageIndex} of {totalImages}] - Retrieved {packages.Count} provisioned AppX packages");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName,
                    $"[{currentImageIndex} of {totalImages}] - Failed to get provisioned AppX packages: {ex.Message}", ex);
                throw;
            }

            return packages;
        }

        /// <summary>
        /// Applies inclusion and exclusion filters to the packages
        /// </summary>
        private void ApplyFilters(List<AppXProvisionedPackage> packages, AppXRemovalResult result)
        {
            foreach (var package in packages)
            {
                // Apply inclusion filter
                if (_compiledInclusionFilter != null)
                {
                    try
                    {
                        package.MatchesInclusionFilter = _compiledInclusionFilter.IsMatch(package.DisplayName);
                        if (package.MatchesInclusionFilter)
                        {
                            result.PackagesMatchingInclusion++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteWarning(this, $"Error applying inclusion filter '{InclusionFilter}': {ex.Message}");
                        package.MatchesInclusionFilter = false;
                    }
                }
                else
                {
                    // No inclusion filter means include all
                    package.MatchesInclusionFilter = true;
                    result.PackagesMatchingInclusion++;
                }

                // Apply exclusion filter
                if (_compiledExclusionFilter != null)
                {
                    try
                    {
                        package.MatchesExclusionFilter = _compiledExclusionFilter.IsMatch(package.DisplayName);
                        if (package.MatchesExclusionFilter)
                        {
                            result.PackagesMatchingExclusion++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteWarning(this, $"Error applying exclusion filter '{ExclusionFilter}': {ex.Message}");
                        package.MatchesExclusionFilter = false;
                    }
                }
                else
                {
                    // No exclusion filter means exclude none
                    package.MatchesExclusionFilter = false;
                }
            }
        }

        /// <summary>
        /// Removes the specified packages from the mounted image
        /// </summary>
        private void RemovePackages(DismSession session, List<AppXProvisionedPackage> packagesToRemove, 
            AppXRemovalResult result, int currentImageIndex, int totalImages)
        {
            for (int i = 0; i < packagesToRemove.Count; i++)
            {
                var package = packagesToRemove[i];
                var packageProgress = (int)((double)(i + 1) / packagesToRemove.Count * 100);

                LoggingService.WriteProgress(this, "Removing AppX Provisioned Packages",
                    $"[{currentImageIndex} of {totalImages}] - {result.MountedImage.ImageName}",
                    $"Removing package {i + 1} of {packagesToRemove.Count}: {package.DisplayName} ({packageProgress}%)",
                    packageProgress);

                try
                {
                    LoggingService.WriteVerbose(this,
                        $"[{currentImageIndex} of {totalImages}] - Removing AppX package: {package.DisplayName} ({package.PackageName})");

                    // Remove the provisioned AppX package
                    DismApi.RemoveProvisionedAppxPackage(session, package.PackageName);

                    result.SuccessfullyRemovedPackages.Add(package);
                    LoggingService.WriteVerbose(this,
                        $"[{currentImageIndex} of {totalImages}] - Successfully removed: {package.DisplayName}");
                }
                catch (Exception ex)
                {
                    result.FailedToRemovePackages.Add(package);
                    result.ErrorMessages[package.PackageName] = ex.Message;

                    var errorAction = ActionPreference.Continue;
                    if (MyInvocation.BoundParameters.ContainsKey("ErrorAction"))
                    {
                        errorAction = (ActionPreference)MyInvocation.BoundParameters["ErrorAction"];
                    }
                    else
                    {
                        errorAction = this.GetVariableValue("ErrorActionPreference") as ActionPreference? ?? ActionPreference.Continue;
                    }

                    if (errorAction == ActionPreference.Stop)
                    {
                        LoggingService.WriteError(this, ComponentName,
                            $"[{currentImageIndex} of {totalImages}] - Failed to remove {package.DisplayName}: {ex.Message}", ex);
                        throw;
                    }
                    else
                    {
                        LoggingService.WriteWarning(this,
                            $"[{currentImageIndex} of {totalImages}] - Failed to remove {package.DisplayName}: {ex.Message}");
                    }
                }
            }
        }
    }
}
