using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Microsoft.Dism;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Unified cmdlet for installing Windows updates into mounted Windows images
    /// Supports both file paths and WindowsUpdatePackage objects with MountedWindowsImage objects
    /// </summary>
    [Cmdlet(VerbsLifecycle.Install, "WindowsImageUpdate")]
    [OutputType(typeof(MountedWindowsImage[]))]
    [OutputType(typeof(WindowsImageUpdateResult[]))]
    public class InstallWindowsImageUpdateCmdlet : PSCmdlet
    {
        /// <summary>
        /// Mounted Windows images to install updates on (from Mount-WindowsImageList)
        /// </summary>
        [Parameter(
            Position = 0,
            Mandatory = true,
            ValueFromPipeline = true,
            ParameterSetName = "FromPackages",
            HelpMessage = "Mounted Windows images from Mount-WindowsImageList")]
        [ValidateNotNull]
        public MountedWindowsImage[] MountedImages { get; set; } = null!;

        /// <summary>
        /// Windows Update packages to install (from Save-WindowsUpdateCatalogResult pipeline)
        /// </summary>
        [Parameter(
            Position = 1,
            Mandatory = true,
            ValueFromPipeline = true,
            ParameterSetName = "FromPackages",
            HelpMessage = "Windows Update packages from Save-WindowsUpdateCatalogResult")]
        [ValidateNotNull]
        public WindowsUpdatePackage[] UpdatePackages { get; set; } = null!;

        /// <summary>
        /// Path to the update file (CAB or MSU) or directory containing updates
        /// </summary>
        [Parameter(
            Position = 0,
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            ParameterSetName = "FromFiles",
            HelpMessage = "Path to the update file (CAB/MSU) or directory containing updates")]
        [ValidateNotNullOrEmpty]
        public FileSystemInfo[] UpdatePath { get; set; } = Array.Empty<FileSystemInfo>();

        /// <summary>
        /// Path to the mounted Windows image directory
        /// </summary>
        [Parameter(
            Position = 1,
            Mandatory = true,
            ValueFromPipelineByPropertyName = true,
            ParameterSetName = "FromFiles",
            HelpMessage = "Path to the mounted Windows image directory")]
        [ValidateNotNullOrEmpty]
        public DirectoryInfo ImagePath { get; set; } = null!;

        /// <summary>
        /// Prevents DISM from checking the applicability of the package
        /// </summary>
        [Parameter(HelpMessage = "Prevents DISM from checking the applicability of the package")]
        public SwitchParameter IgnoreCheck { get; set; }

        /// <summary>
        /// Prevents the automatic installation of prerequisite packages
        /// </summary>
        [Parameter(HelpMessage = "Prevents the automatic installation of prerequisite packages")]
        public SwitchParameter PreventPending { get; set; }

        /// <summary>
        /// Continues processing other updates even if one fails
        /// </summary>
        [Parameter(HelpMessage = "Continues processing other updates even if one fails")]
        public SwitchParameter ContinueOnError { get; set; }

        /// <summary>
        /// Validates that the image is suitable for update integration
        /// </summary>
        [Parameter(
            ParameterSetName = "FromFiles",
            HelpMessage = "Validates that the image is suitable for update integration")]
        public SwitchParameter ValidateImage { get; set; }

        private const string ComponentName = "WindowsImageUpdate";
        private readonly List<MountedWindowsImage> _allMountedImages = new List<MountedWindowsImage>();
        private readonly List<WindowsUpdatePackage> _allUpdatePackages = new List<WindowsUpdatePackage>();
        private readonly List<FileSystemInfo> _allUpdatePaths = new List<FileSystemInfo>();

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                // Collect data from pipeline based on parameter set
                if (ParameterSetName == "FromPackages")
                {
                    _allMountedImages.AddRange(MountedImages);
                    _allUpdatePackages.AddRange(UpdatePackages);
                }
                else if (ParameterSetName == "FromFiles")
                {
                    _allUpdatePaths.AddRange(UpdatePath);
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, $"Failed to process record: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Processes all collected data at the end of the pipeline
        /// </summary>
        protected override void EndProcessing()
        {
            try
            {
                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName, "Install Windows Image Updates");

                if (ParameterSetName == "FromPackages")
                {
                    ProcessPackageInstallation(operationStartTime);
                }
                else if (ParameterSetName == "FromFiles")
                {
                    ProcessFileInstallation(operationStartTime);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogOperationFailure(this, ComponentName, ex);
                ThrowTerminatingError(new ErrorRecord(
                    ex,
                    "InstallWindowsImageUpdateFailed",
                    ErrorCategory.NotSpecified,
                    null));
            }
        }

        /// <summary>
        /// Processes installation from WindowsUpdatePackage objects
        /// </summary>
        private void ProcessPackageInstallation(DateTime operationStartTime)
        {
            if (_allMountedImages.Count == 0 || _allUpdatePackages.Count == 0)
            {
                WriteWarning("No mounted images or update packages found to process");
                return;
            }

            var updatedImages = InstallPackagesOnMountedImages(_allUpdatePackages, _allMountedImages);

            // Output updated mounted images for pipeline
            foreach (var mountedImage in updatedImages)
            {
                WriteObject(mountedImage);
            }

            // Summary
            var successCount = updatedImages.Count(m => m.LastUpdateResult?.Success == true);
            var failureCount = updatedImages.Count(m => m.LastUpdateResult?.Success == false);

            LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Install Windows Updates", operationStartTime,
                $"Completed: {successCount} successful, {failureCount} failed installations");

            if (failureCount > 0)
            {
                WriteWarning($"{failureCount} update installations failed. Check the LastUpdateResult property for details.");
            }
        }

        /// <summary>
        /// Processes installation from file paths
        /// </summary>
        private void ProcessFileInstallation(DateTime operationStartTime)
        {
            // Validate image path
            if (!ImagePath.Exists)
            {
                var errorMessage = $"Image path does not exist: {ImagePath.FullName}";
                LoggingService.WriteError(this, ComponentName, errorMessage);
                ThrowTerminatingError(new ErrorRecord(
                    new DirectoryNotFoundException(errorMessage),
                    "ImagePathNotFound",
                    ErrorCategory.ObjectNotFound,
                    ImagePath.FullName));
                return;
            }

            // Validate image if requested
            if (ValidateImage.IsPresent)
            {
                ValidateMountedImage();
            }

            // Collect all update files
            var updateFiles = CollectUpdateFiles();
            LoggingService.WriteVerbose(this, $"Found {updateFiles.Count} update file(s) to install");

            if (updateFiles.Count == 0)
            {
                WriteWarning("No update files found to install");
                return;
            }

            // Install updates
            var results = new List<WindowsImageUpdateResult>();
            var successCount = 0;
            var failureCount = 0;

            for (int i = 0; i < updateFiles.Count; i++)
            {
                var updateFile = updateFiles[i];
                var currentIndex = i + 1;

                try
                {
                    LoggingService.WriteVerbose(this, $"[{currentIndex} of {updateFiles.Count}] Installing {updateFile.Name}");

                    var result = InstallSingleUpdate(updateFile, currentIndex, updateFiles.Count);
                    results.Add(result);

                    if (result.IsSuccessful)
                    {
                        successCount++;
                        LoggingService.WriteVerbose(this, $"[{currentIndex} of {updateFiles.Count}] Successfully installed: {updateFile.Name}");
                    }
                    else
                    {
                        failureCount++;
                        LoggingService.WriteWarning(this, ComponentName, $"[{currentIndex} of {updateFiles.Count}] Failed to install: {updateFile.Name} - {result.ErrorMessage}");

                        if (!ContinueOnError.IsPresent)
                        {
                            WriteError(new ErrorRecord(
                                new InvalidOperationException(result.ErrorMessage),
                                "UpdateInstallationFailed",
                                ErrorCategory.InvalidOperation,
                                updateFile.FullName));
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    var errorMessage = $"Failed to install {updateFile.Name}: {ex.Message}";
                    LoggingService.WriteError(this, ComponentName, errorMessage, ex);

                    var result = new WindowsImageUpdateResult
                    {
                        UpdateFile = updateFile,
                        ImagePath = ImagePath,
                        IsSuccessful = false,
                        ErrorMessage = errorMessage,
                        InstallationTime = DateTime.UtcNow
                    };
                    results.Add(result);

                    if (!ContinueOnError.IsPresent)
                    {
                        WriteError(new ErrorRecord(ex, "UpdateInstallationException", ErrorCategory.InvalidOperation, updateFile.FullName));
                        break;
                    }
                }
            }

            // Output results
            foreach (var result in results)
            {
                WriteObject(result);
            }

            // Final statistics
            var totalCount = updateFiles.Count;
            var successPercentage = totalCount > 0 ? Math.Round((double)successCount / totalCount * 100, 1) : 0;
            var failurePercentage = totalCount > 0 ? Math.Round((double)failureCount / totalCount * 100, 1) : 0;

            LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Install Windows Image Updates", operationStartTime,
                $"Completed {totalCount} update installation(s)");

            LoggingService.WriteVerbose(this, $"Succeeded: {successCount} of {totalCount} ({successPercentage}%)");
            LoggingService.WriteVerbose(this, $"Failed: {failureCount} of {totalCount} ({failurePercentage}%)");

            if (failureCount > 0)
            {
                WriteWarning($"{failureCount} update installations failed. Check the ErrorMessage property for details.");
            }
        }

        /// <summary>
        /// Validates that the mounted image is suitable for update integration
        /// </summary>
        private void ValidateMountedImage()
        {
            LoggingService.WriteVerbose(this, "Validating mounted image...");

            // Check for Windows directory
            var windowsDir = new DirectoryInfo(Path.Combine(ImagePath.FullName, "Windows"));
            if (!windowsDir.Exists)
            {
                throw new InvalidOperationException($"Windows directory not found in mounted image: {windowsDir.FullName}");
            }

            // Check for System32 directory
            var system32Dir = new DirectoryInfo(Path.Combine(windowsDir.FullName, "System32"));
            if (!system32Dir.Exists)
            {
                throw new InvalidOperationException($"System32 directory not found in mounted image: {system32Dir.FullName}");
            }

            LoggingService.WriteVerbose(this, "Image validation completed successfully");
        }

        /// <summary>
        /// Installs update packages on mounted images using proper DISM API
        /// </summary>
        private List<MountedWindowsImage> InstallPackagesOnMountedImages(List<WindowsUpdatePackage> packages, List<MountedWindowsImage> mountedImages)
        {
            var updatedImages = new List<MountedWindowsImage>();
            var progressRecord = new ProgressRecord(1, "Installing Windows Updates", "Preparing installation...");
            WriteProgress(progressRecord);

            var totalOperations = packages.Count * mountedImages.Count;
            var currentOperation = 0;

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

            progressRecord.RecordType = ProgressRecordType.Completed;
            WriteProgress(progressRecord);

            return updatedImages;
        }

        /// <summary>
        /// Installs a single package on a mounted image
        /// </summary>
        private MountedWindowsImage InstallPackageOnMountedImage(WindowsUpdatePackage package, MountedWindowsImage mountedImage)
        {
            var result = new UpdateInstallationResult
            {
                Update = new WindowsUpdate { KBNumber = package.KBNumber, Title = package.Title },
                Success = false,
                StartTime = DateTime.UtcNow
            };

            try
            {
                LoggingService.WriteVerbose(this,
                    $"Installing {package.KBNumber} on mounted image {mountedImage.ImageName} at {mountedImage.MountPath?.FullName ?? "Unknown"}");

                // Use proper DISM API to install the package on the mounted image
                if (mountedImage.MountPath == null)
                    throw new InvalidOperationException("Mount path is null");
                using var session = Microsoft.Dism.DismApi.OpenOfflineSession(mountedImage.MountPath.FullName);
                Microsoft.Dism.DismApi.AddPackage(session, package.LocalFile.FullName, IgnoreCheck.IsPresent, PreventPending.IsPresent);

                result.Success = true;
                result.ExitCode = 0;
                result.LogOutput = $"Successfully installed {package.KBNumber} using DISM API";

                LoggingService.WriteVerbose(this,
                    $"Successfully installed {package.KBNumber} on {mountedImage.ImageName}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ExitCode = -1;
                result.ErrorMessage = ex.Message;
                result.LogOutput = $"Failed to install {package.KBNumber}: {ex.Message}";

                LoggingService.WriteError(this, ComponentName,
                    $"Failed to install {package.KBNumber} on {mountedImage.ImageName}: {ex.Message}", ex);

                if (!ContinueOnError.IsPresent)
                {
                    throw;
                }
            }
            finally
            {
                result.EndTime = DateTime.UtcNow;
            }

            // Create updated mounted image with result
            var updatedImage = new MountedWindowsImage
            {
                MountId = mountedImage.MountId,
                SourceImagePath = mountedImage.SourceImagePath,
                ImageName = mountedImage.ImageName,
                ImageIndex = mountedImage.ImageIndex,
                Edition = mountedImage.Edition,
                MountPath = mountedImage.MountPath,
                Status = mountedImage.Status,
                Architecture = mountedImage.Architecture,
                WimGuid = mountedImage.WimGuid,
                MountedAt = mountedImage.MountedAt,
                IsReadOnly = mountedImage.IsReadOnly,
                ErrorMessage = mountedImage.ErrorMessage,
                ImageSize = mountedImage.ImageSize,
                LastUpdateResult = result
            };

            return updatedImage;
        }

        /// <summary>
        /// Collects all update files from the specified paths
        /// </summary>
        private List<FileInfo> CollectUpdateFiles()
        {
            var updateFiles = new List<FileInfo>();

            foreach (var path in _allUpdatePaths)
            {
                if (path is FileInfo fileInfo)
                {
                    if (IsValidUpdateFile(fileInfo))
                    {
                        updateFiles.Add(fileInfo);
                    }
                    else
                    {
                        WriteWarning($"Skipping invalid update file: {fileInfo.FullName}");
                    }
                }
                else if (path is DirectoryInfo directoryInfo)
                {
                    if (directoryInfo.Exists)
                    {
                        var files = directoryInfo.GetFiles("*.cab", SearchOption.TopDirectoryOnly)
                            .Concat(directoryInfo.GetFiles("*.msu", SearchOption.TopDirectoryOnly))
                            .Where(IsValidUpdateFile)
                            .ToList();

                        updateFiles.AddRange(files);
                        LoggingService.WriteVerbose(this, $"Found {files.Count} update files in directory: {directoryInfo.FullName}");
                    }
                    else
                    {
                        WriteWarning($"Directory does not exist: {directoryInfo.FullName}");
                    }
                }
            }

            return updateFiles.OrderBy(f => f.Name).ToList();
        }

        /// <summary>
        /// Validates that a file is a valid update file
        /// </summary>
        private static bool IsValidUpdateFile(FileInfo file)
        {
            if (!file.Exists) return false;

            var extension = file.Extension.ToLowerInvariant();
            return extension == ".cab" || extension == ".msu";
        }

        /// <summary>
        /// Installs a single update into the mounted image
        /// </summary>
        private WindowsImageUpdateResult InstallSingleUpdate(FileInfo updateFile, int currentIndex, int totalCount)
        {
            var result = new WindowsImageUpdateResult
            {
                UpdateFile = updateFile,
                ImagePath = ImagePath,
                InstallationTime = DateTime.UtcNow
            };

            try
            {
                LoggingService.WriteVerbose(this, $"Installing update: {updateFile.FullName}");

                // Create progress callback
                var progressCallback = ProgressService.CreateInstallProgressCallback(
                    this,
                    "Installing Windows Updates",
                    updateFile.Name,
                    currentIndex,
                    totalCount);

                // Install the update using DISM
                using (var session = Microsoft.Dism.DismApi.OpenOfflineSession(ImagePath.FullName))
                {
                    // Install the package with options
                    Microsoft.Dism.DismApi.AddPackage(session, updateFile.FullName, IgnoreCheck.IsPresent, PreventPending.IsPresent);

                    result.IsSuccessful = true;
                    LoggingService.WriteVerbose(this, $"Successfully installed update: {updateFile.Name}");
                }

                progressCallback?.Invoke(100, $"Completed installation of {updateFile.Name}");
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = ex.Message;
                LoggingService.WriteError(this, ComponentName, $"Failed to install {updateFile.Name}: {ex.Message}", ex);
            }

            return result;
        }
    }
}
