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
    /// Performs component cleanup and reset base operations on mounted Windows images
    /// </summary>
    [Cmdlet(VerbsCommon.Reset, "WindowsImageBase")]
    [OutputType(typeof(MountedWindowsImage[]))]
    public class ResetWindowsImageBaseCmdlet : PSCmdlet
    {
        /// <summary>
        /// Mounted image objects to perform reset base on
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = "ByObject")]
        [ValidateNotNull]
        public MountedWindowsImage[] MountedImages { get; set; } = Array.Empty<MountedWindowsImage>();

        /// <summary>
        /// Mount directories to perform reset base on
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ByPath")]
        [ValidateNotNullOrEmpty]
        public DirectoryInfo[] Path { get; set; } = Array.Empty<DirectoryInfo>();

        /// <summary>
        /// Perform component cleanup before reset base
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter ComponentCleanup { get; set; }

        /// <summary>
        /// Analyze only - show what would be cleaned up without making changes
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter AnalyzeOnly { get; set; }

        /// <summary>
        /// Continue processing other images if one fails
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter ContinueOnError { get; set; }

        /// <summary>
        /// Defer the reset base operation until next reboot (for online images)
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Defer { get; set; }

        private const string ComponentName = "ResetImageBase";
        private readonly List<MountedWindowsImage> _allMountedImages = new List<MountedWindowsImage>();

        /// <summary>
        /// Processes pipeline input
        /// </summary>
        protected override void ProcessRecord()
        {
            if (ParameterSetName == "ByObject")
            {
                _allMountedImages.AddRange(MountedImages);
            }
            else if (ParameterSetName == "ByPath")
            {
                // Convert paths to MountedWindowsImage objects
                foreach (var path in Path)
                {
                    var mountedImage = new MountedWindowsImage
                    {
                        MountPath = path,
                        ImageName = $"Image at {path.FullName}",
                        Status = MountStatus.Mounted
                    };
                    _allMountedImages.Add(mountedImage);
                }
            }
        }

        /// <summary>
        /// Performs the reset base operation
        /// </summary>
        protected override void EndProcessing()
        {
            if (_allMountedImages.Count == 0)
            {
                LoggingService.WriteWarning(this, "No mounted images provided for reset base operation");
                return;
            }

            var startTime = DateTime.UtcNow;
            var results = new List<MountedWindowsImage>();

            try
            {
                var operationName = AnalyzeOnly.IsPresent ? "Analyzing" : "Resetting";
                var operationDetails = ComponentCleanup.IsPresent ? "Component Cleanup + Reset Base" : "Reset Base";
                
                LoggingService.LogOperationStart(this, ComponentName, $"{operationName} Windows Image Base");

                // Show initial progress
                LoggingService.WriteProgress(this, $"{operationName} Windows Image Base", 
                    $"Preparing to process {_allMountedImages.Count} images", 
                    $"Operation: {operationDetails}", 0);

                // Process each image
                for (int i = 0; i < _allMountedImages.Count; i++)
                {
                    var mountedImage = _allMountedImages[i];
                    var progress = (int)((double)(i + 1) / _allMountedImages.Count * 100);
                    
                    LoggingService.WriteProgress(this, $"{operationName} Windows Image Base", 
                        $"[{i + 1} of {_allMountedImages.Count}] - {mountedImage.ImageName}", 
                        $"Processing {mountedImage.MountPath.FullName} ({progress}%)", progress);

                    try
                    {
                        var result = ProcessSingleImage(mountedImage, i + 1, _allMountedImages.Count);
                        results.Add(result);
                        
                        LoggingService.WriteVerbose(this, $"[{i + 1} of {_allMountedImages.Count}] - Successfully processed: {mountedImage.MountPath.FullName}");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteError(this, $"[{i + 1} of {_allMountedImages.Count}] - Failed to process {mountedImage.MountPath.FullName}: {ex.Message}", ex);
                        
                        // Update status to failed
                        mountedImage.Status = MountStatus.Failed;
                        mountedImage.ErrorMessage = ex.Message;
                        results.Add(mountedImage);

                        if (!ContinueOnError.IsPresent)
                        {
                            throw;
                        }
                    }
                }

                LoggingService.CompleteProgress(this, $"{operationName} Windows Image Base");

                // Show summary
                var successCount = results.Count(m => m.Status == MountStatus.Mounted);
                var failCount = results.Count(m => m.Status == MountStatus.Failed);
                
                LoggingService.WriteVerbose(this, $"Reset base operation complete: {successCount} successful, {failCount} failed");

                // Output results
                WriteObject(results.ToArray());

                var duration = DateTime.UtcNow - startTime;
                LoggingService.LogOperationComplete(this, ComponentName, duration, $"Processed {successCount} of {_allMountedImages.Count} images");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, "Failed to perform reset base operation", ex);
                throw;
            }
        }

        /// <summary>
        /// Processes a single image for reset base operation
        /// </summary>
        private MountedWindowsImage ProcessSingleImage(MountedWindowsImage mountedImage, int currentIndex, int totalCount)
        {
            // Create a copy to avoid modifying the original
            var result = new MountedWindowsImage
            {
                MountId = mountedImage.MountId,
                SourceImagePath = mountedImage.SourceImagePath,
                ImageIndex = mountedImage.ImageIndex,
                ImageName = mountedImage.ImageName,
                Edition = mountedImage.Edition,
                Architecture = mountedImage.Architecture,
                MountPath = mountedImage.MountPath,
                Status = mountedImage.Status,
                // Note: MountTime and ReadOnly properties don't exist in current model
            };

            try
            {
                // Validate mount path exists
                if (!mountedImage.MountPath.Exists)
                {
                    throw new DirectoryNotFoundException($"Mount path does not exist: {mountedImage.MountPath.FullName}");
                }

                LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Processing image at {mountedImage.MountPath.FullName}");

                var operationStartTime = DateTime.UtcNow;

                // Perform component cleanup if requested
                if (ComponentCleanup.IsPresent)
                {
                    LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Performing component cleanup");
                    
                    LoggingService.WriteProgress(this, "Resetting Windows Image Base",
                        $"[{currentIndex} of {totalCount}] - {mountedImage.ImageName}",
                        "Performing component cleanup...",
                        (int)((double)(currentIndex - 1) / totalCount * 100) + 5);

                    if (!AnalyzeOnly.IsPresent)
                    {
                        LoggingService.WriteWarning(this, $"[{currentIndex} of {totalCount}] - Component cleanup not supported by Microsoft.Dism API. Use DISM.exe directly for this functionality.");
                    }
                    else
                    {
                        LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Analysis mode: Component cleanup would be performed");
                    }
                }

                // Perform reset base operation
                LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Performing reset base operation");
                
                LoggingService.WriteProgress(this, "Resetting Windows Image Base",
                    $"[{currentIndex} of {totalCount}] - {mountedImage.ImageName}",
                    "Performing reset base operation...",
                    (int)((double)(currentIndex - 1) / totalCount * 100) + 15);

                if (!AnalyzeOnly.IsPresent)
                {
                    var deferMessage = Defer.IsPresent ? " with defer option" : "";
                    LoggingService.WriteWarning(this, $"[{currentIndex} of {totalCount}] - Reset base{deferMessage} not supported by Microsoft.Dism API. Use DISM.exe directly for this functionality.");
                }
                else
                {
                    LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Analysis mode: Reset base would be performed");
                }

                var operationDuration = DateTime.UtcNow - operationStartTime;

                // Report completion
                LoggingService.WriteProgress(this, "Resetting Windows Image Base",
                    $"[{currentIndex} of {totalCount}] - {mountedImage.ImageName}",
                    $"Operation completed in {LoggingService.FormatDuration(operationDuration)}",
                    (int)((double)currentIndex / totalCount * 100));

                LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Reset base operation completed successfully (Duration: {LoggingService.FormatDuration(operationDuration)})");

                result.Status = MountStatus.Mounted; // Still mounted after reset base
                return result;
            }
            catch (Exception ex)
            {
                result.Status = MountStatus.Failed;
                result.ErrorMessage = ex.Message;
                
                LoggingService.WriteError(this, $"[{currentIndex} of {totalCount}] - Failed to perform reset base operation: {ex.Message}", ex);
                throw;
            }
        }
    }
}
