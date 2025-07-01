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
    /// Cmdlet for installing Windows updates (CAB/MSU files) into mounted Windows images from file paths
    /// </summary>
    [Cmdlet(VerbsLifecycle.Install, "WindowsUpdateFile")]
    [OutputType(typeof(WindowsImageUpdateResult[]))]
    public class InstallWindowsUpdateFileCmdlet : PSCmdlet
    {
        /// <summary>
        /// Path to the update file (CAB or MSU) or directory containing updates
        /// </summary>
        [Parameter(
            Position = 0,
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
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
            HelpMessage = "Path to the mounted Windows image directory")]
        [ValidateNotNullOrEmpty]
        public DirectoryInfo ImagePath { get; set; } = null!;

        /// <summary>
        /// Prevents DISM from checking the applicability of the package
        /// </summary>
        [Parameter(
            HelpMessage = "Prevents DISM from checking the applicability of the package")]
        public SwitchParameter IgnoreCheck { get; set; }

        /// <summary>
        /// Prevents the automatic installation of prerequisite packages
        /// </summary>
        [Parameter(
            HelpMessage = "Prevents the automatic installation of prerequisite packages")]
        public SwitchParameter PreventPending { get; set; }

        /// <summary>
        /// Continues processing other updates even if one fails
        /// </summary>
        [Parameter(
            HelpMessage = "Continues processing other updates even if one fails")]
        public SwitchParameter ContinueOnError { get; set; }

        /// <summary>
        /// Validates that the image is suitable for update integration
        /// </summary>
        [Parameter(
            HelpMessage = "Validates that the image is suitable for update integration")]
        public SwitchParameter ValidateImage { get; set; }

        private const string ComponentName = "WindowsImageUpdate";

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName, "Install Windows Image Updates");

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
        /// Collects all update files from the specified paths
        /// </summary>
        private List<FileInfo> CollectUpdateFiles()
        {
            var updateFiles = new List<FileInfo>();

            foreach (var path in UpdatePath)
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
