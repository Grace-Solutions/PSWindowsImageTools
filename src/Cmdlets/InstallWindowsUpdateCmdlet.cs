using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Installs Windows Updates on mounted Windows images from Mount-WindowsImageList
    /// </summary>
    [Cmdlet(VerbsLifecycle.Install, "WindowsImageUpdate")]
    [OutputType(typeof(MountedWindowsImage))]
    public class InstallWindowsImageUpdateCmdlet : PSCmdlet
    {
        /// <summary>
        /// Mounted Windows images to install updates on (from Mount-WindowsImageList)
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
        [ValidateNotNull]
        public MountedWindowsImage[] MountedImages { get; set; } = null!;

        /// <summary>
        /// Windows Update packages to install (from Save-WindowsUpdateCatalogResult pipeline)
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true, ParameterSetName = "FromPipeline")]
        [ValidateNotNull]
        public WindowsUpdatePackage[] InputObject { get; set; } = null!;

        /// <summary>
        /// Windows Update packages to install (from parameter)
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "FromParameter")]
        [ValidateNotNull]
        public WindowsUpdatePackage[] UpdatePackages { get; set; } = null!;

        /// <summary>
        /// Continue on error instead of stopping
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter ContinueOnError { get; set; }

        private readonly List<MountedWindowsImage> _allMountedImages = new List<MountedWindowsImage>();
        private readonly List<WindowsUpdatePackage> _allUpdatePackages = new List<WindowsUpdatePackage>();
        private const string ComponentName = "WindowsUpdateInstallation";

        protected override void ProcessRecord()
        {
            try
            {
                // Collect mounted images from pipeline
                _allMountedImages.AddRange(MountedImages);

                // Collect update packages from pipeline or parameter
                var packagesToProcess = ParameterSetName == "FromPipeline" ? InputObject : UpdatePackages;
                _allUpdatePackages.AddRange(packagesToProcess);
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, $"Failed to process record: {ex.Message}", ex);
                throw;
            }
        }

        protected override void EndProcessing()
        {
            try
            {
                if (_allUpdatePackages.Count == 0)
                {
                    WriteWarning("No update packages provided for installation");
                    return;
                }

                if (_allMountedImages.Count == 0)
                {
                    WriteWarning("No mounted images provided for installation");
                    return;
                }

                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName,
                    "Install Windows Updates", $"{_allUpdatePackages.Count} packages on {_allMountedImages.Count} mounted images");

                LoggingService.WriteVerbose(this, $"Installing {_allUpdatePackages.Count} update packages on {_allMountedImages.Count} mounted images");

                // Validate that packages are downloaded
                var downloadedPackages = _allUpdatePackages.Where(p => p.IsDownloaded).ToList();
                var notDownloadedPackages = _allUpdatePackages.Where(p => !p.IsDownloaded).ToList();

                if (notDownloadedPackages.Any())
                {
                    WriteWarning($"{notDownloadedPackages.Count} packages are not downloaded and will be skipped:");
                    foreach (var package in notDownloadedPackages)
                    {
                        WriteWarning($"  {package.KBNumber} - {package.Title}");
                    }
                }

                if (!downloadedPackages.Any())
                {
                    WriteWarning("No downloaded packages available for installation");
                    return;
                }

                // Install packages on mounted images
                var updatedMountedImages = InstallPackagesOnMountedImages(downloadedPackages, _allMountedImages);

                // Output updated mounted images for pipeline
                foreach (var mountedImage in updatedMountedImages)
                {
                    WriteObject(mountedImage);
                }

                // Summary
                var successCount = updatedMountedImages.Count(m => m.LastUpdateResult?.Success == true);
                var failureCount = updatedMountedImages.Count(m => m.LastUpdateResult?.Success == false);

                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Install Windows Updates", operationStartTime,
                    $"Completed: {successCount} successful, {failureCount} failed installations");

                if (failureCount > 0)
                {
                    WriteWarning($"{failureCount} update installations failed. Check the LastUpdateResult property for details.");
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, $"Windows Update installation failed: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Installs update packages on mounted images using proper DISM API
        /// </summary>
        private List<MountedWindowsImage> InstallPackagesOnMountedImages(List<WindowsUpdatePackage> packages, List<MountedWindowsImage> mountedImages)
        {
            var updatedImages = new List<MountedWindowsImage>();
            var progressRecord = new ProgressRecord(1, "Installing Windows Updates", "Preparing installation...");
            WriteProgress(progressRecord);

            try
            {
                int totalOperations = packages.Count * mountedImages.Count;
                int currentOperation = 0;

                foreach (var mountedImage in mountedImages)
                {
                    // Skip images that are not properly mounted
                    if (mountedImage.Status != MountStatus.Mounted)
                    {
                        LoggingService.WriteWarning(this, $"Skipping image {mountedImage.ImageName} - not properly mounted (Status: {mountedImage.Status})");
                        updatedImages.Add(mountedImage);
                        continue;
                    }

                    foreach (var package in packages)
                    {
                        currentOperation++;
                        var percentage = (int)((double)currentOperation / totalOperations * 100);

                        progressRecord.StatusDescription = $"Installing {package.KBNumber} on {mountedImage.ImageName}";
                        progressRecord.PercentComplete = percentage;
                        WriteProgress(progressRecord);

                        var updatedImage = InstallPackageOnMountedImage(package, mountedImage);
                        updatedImages.Add(updatedImage);
                    }
                }
            }
            finally
            {
                progressRecord.RecordType = ProgressRecordType.Completed;
                WriteProgress(progressRecord);
            }

            return updatedImages;
        }

        /// <summary>
        /// Installs a single update package on a mounted image using proper DISM API
        /// </summary>
        private MountedWindowsImage InstallPackageOnMountedImage(WindowsUpdatePackage package, MountedWindowsImage mountedImage)
        {
            // Create a copy to avoid modifying the original
            var updatedImage = new MountedWindowsImage
            {
                MountId = mountedImage.MountId,
                SourceImagePath = mountedImage.SourceImagePath,
                ImageIndex = mountedImage.ImageIndex,
                ImageName = mountedImage.ImageName,
                Edition = mountedImage.Edition,
                Architecture = mountedImage.Architecture,
                MountPath = mountedImage.MountPath,
                WimGuid = mountedImage.WimGuid,
                MountedAt = mountedImage.MountedAt,
                Status = mountedImage.Status,
                IsReadOnly = mountedImage.IsReadOnly,
                ImageSize = mountedImage.ImageSize,
                ErrorMessage = mountedImage.ErrorMessage
            };

            var result = new UpdateInstallationResult
            {
                Update = ConvertPackageToUpdate(package),
                ImagePath = mountedImage.SourceImagePath,
                ImageIndex = mountedImage.ImageIndex,
                StartTime = DateTime.UtcNow
            };

            try
            {
                LoggingService.WriteVerbose(this,
                    $"Installing {package.KBNumber} on mounted image {mountedImage.ImageName} at {mountedImage.MountPath.FullName}");

                // Use proper DISM API to install the package on the mounted image
                using var session = Microsoft.Dism.DismApi.OpenOfflineSession(mountedImage.MountPath.FullName);
                Microsoft.Dism.DismApi.AddPackage(session, package.LocalFile.FullName, true, true);

                result.Success = true;
                result.ExitCode = 0;
                result.LogOutput = $"Successfully installed {package.KBNumber} using DISM API";

                LoggingService.WriteVerbose(this,
                    $"Successfully installed {package.KBNumber} on {mountedImage.ImageName}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.ExitCode = -1;

                LoggingService.WriteError(this, ComponentName,
                    $"Failed to install {package.KBNumber} on {mountedImage.ImageName}: {ex.Message}", ex);

                if (!ContinueOnError.IsPresent)
                {
                    updatedImage.Status = MountStatus.Failed;
                    updatedImage.ErrorMessage = ex.Message;
                }
            }
            finally
            {
                result.EndTime = DateTime.UtcNow;
                updatedImage.LastUpdateResult = result;
            }

            return updatedImage;
        }

        /// <summary>
        /// Converts WindowsUpdatePackage to WindowsUpdate for compatibility with UpdateInstallationResult
        /// </summary>
        private WindowsUpdate ConvertPackageToUpdate(WindowsUpdatePackage package)
        {
            return new WindowsUpdate
            {
                UpdateId = package.UpdateId,
                KBNumber = package.KBNumber,
                Title = package.Title,
                Products = string.Join(", ", package.SourceCatalogResult.Products),
                Classification = package.SourceCatalogResult.Classification,
                LastUpdated = package.SourceCatalogResult.LastModified,
                SizeInBytes = package.SourceCatalogResult.Size,
                DownloadUrls = package.SourceCatalogResult.DownloadUrls.Select(uri => uri.OriginalString).ToList(),
                Architecture = package.SourceCatalogResult.Architecture,
                LocalFilePath = package.LocalFile.FullName
            };
        }
    }
}
