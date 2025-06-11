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
    /// Mounts Windows images from WIM/ESD files with progress reporting
    /// </summary>
    [Cmdlet(VerbsData.Mount, "WindowsImageList")]
    [OutputType(typeof(MountedWindowsImage[]))]
    public class MountWindowsImageListCmdlet : PSCmdlet
    {
        /// <summary>
        /// Path to the WIM or ESD file
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public FileInfo ImagePath { get; set; } = null!;

        /// <summary>
        /// Inclusion filter scriptblock to select which images to mount (e.g., {$_.Name -like "*Pro*"} or {$_.Index -eq 1})
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNull]
        public ScriptBlock? InclusionFilter { get; set; }

        /// <summary>
        /// Exclusion filter scriptblock to exclude images from mounting (e.g., {$_.Name -like "*Enterprise*"})
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNull]
        public ScriptBlock? ExclusionFilter { get; set; }

        /// <summary>
        /// Mount images as read-write (default is read-only)
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter ReadWrite { get; set; }

        /// <summary>
        /// Custom mount root directory (uses temp if not specified)
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string? MountRoot { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            var startTime = DateTime.UtcNow;
            var mountedImages = new List<MountedWindowsImage>();

            try
            {
                LoggingService.LogOperationStart(this, "MountImageList", $"Mounting images from: {ImagePath.FullName}");

                // Validate input file
                if (!ImagePath.Exists)
                {
                    LoggingService.WriteError(this, $"Image file not found: {ImagePath.FullName}");
                    return;
                }

                // Get mount root directory
                var mountRoot = MountRoot ?? ConfigurationService.DefaultMountRootDirectory;
                LoggingService.WriteVerbose(this, $"Using mount root directory: {mountRoot}");

                // Initialize DISM service
                using var dismService = new DismService();
                var imageFilePath = ImagePath.FullName;

                LoggingService.WriteVerbose(this, $"Getting image list from: {imageFilePath}");

                // Get list of images in the file
                var imageInfoList = dismService.GetImageInfo(imageFilePath, this);

                if (imageInfoList.Count == 0)
                {
                    LoggingService.WriteWarning(this, "No images found in the specified file");
                    return;
                }

                LoggingService.WriteVerbose(this, $"Found {imageInfoList.Count} images in file");

                // Apply inclusion filter first (if provided)
                if (InclusionFilter != null)
                {
                    var includedImages = new List<WindowsImageInfo>();

                    foreach (var imageInfo in imageInfoList)
                    {
                        try
                        {
                            // Create a PSVariable for $_ to pass to the scriptblock
                            var dollarUnder = new PSVariable("_", imageInfo);
                            var results = InclusionFilter.InvokeWithContext(null, new List<PSVariable> { dollarUnder });

                            if (results.Count > 0 && LanguagePrimitives.IsTrue(results[0]))
                            {
                                includedImages.Add(imageInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggingService.WriteWarning(this, $"Inclusion filter evaluation failed for image {imageInfo.Index}: {ex.Message}");
                        }
                    }

                    imageInfoList = includedImages;
                    LoggingService.WriteVerbose(this, $"Inclusion filter applied: {imageInfoList.Count} images included");
                }

                // Apply exclusion filter second (if provided)
                if (ExclusionFilter != null)
                {
                    var nonExcludedImages = new List<WindowsImageInfo>();

                    foreach (var imageInfo in imageInfoList)
                    {
                        try
                        {
                            // Create a PSVariable for $_ to pass to the scriptblock
                            var dollarUnder = new PSVariable("_", imageInfo);
                            var results = ExclusionFilter.InvokeWithContext(null, new List<PSVariable> { dollarUnder });

                            // If exclusion filter returns false or null, include the image
                            if (results.Count == 0 || !LanguagePrimitives.IsTrue(results[0]))
                            {
                                nonExcludedImages.Add(imageInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggingService.WriteWarning(this, $"Exclusion filter evaluation failed for image {imageInfo.Index}: {ex.Message}");
                            // On error, include the image (fail-safe)
                            nonExcludedImages.Add(imageInfo);
                        }
                    }

                    imageInfoList = nonExcludedImages;
                    LoggingService.WriteVerbose(this, $"Exclusion filter applied: {imageInfoList.Count} images remaining");
                }

                if (imageInfoList.Count == 0)
                {
                    LoggingService.WriteWarning(this, "No images match the specified filters");
                    return;
                }

                // Generate one GUID per WIM file for mount organization
                var wimGuid = Guid.NewGuid().ToString();

                // Show initial progress
                LoggingService.WriteProgress(this, "Mounting Windows Images", 
                    $"Found {imageInfoList.Count} images to mount", 
                    $"Preparing to mount {imageInfoList.Count} images", 0);

                // Mount each image
                for (int i = 0; i < imageInfoList.Count; i++)
                {
                    var imageInfo = imageInfoList[i];
                    var progress = (int)((double)(i + 1) / imageInfoList.Count * 100);
                    
                    LoggingService.WriteProgress(this, "Mounting Windows Images", 
                        $"[{i + 1} of {imageInfoList.Count}] - {imageInfo.Name}", 
                        $"Mounting Image Index {imageInfo.Index} ({progress}%)", progress);

                    try
                    {
                        var mountedImage = MountSingleImage(imageInfo, mountRoot, wimGuid, i + 1, imageInfoList.Count);
                        mountedImages.Add(mountedImage);
                        
                        LoggingService.WriteVerbose(this, $"[{i + 1} of {imageInfoList.Count}] - Successfully mounted: {mountedImage.MountPath}");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteError(this, $"[{i + 1} of {imageInfoList.Count}] - Failed to mount image {imageInfo.Index}: {ex.Message}", ex);
                        
                        // Create a failed mount object for tracking
                        var failedMount = new MountedWindowsImage
                        {
                            MountId = Guid.NewGuid().ToString(),
                            SourceImagePath = imageFilePath,
                            ImageIndex = imageInfo.Index,
                            ImageName = imageInfo.Name,
                            Edition = imageInfo.Edition,
                            Architecture = imageInfo.Architecture,
                            WimGuid = wimGuid,
                            Status = MountStatus.Failed,
                            ErrorMessage = ex.Message,
                            ImageSize = imageInfo.Size,
                            IsReadOnly = !ReadWrite.IsPresent
                        };
                        mountedImages.Add(failedMount);
                    }
                }

                LoggingService.CompleteProgress(this, "Mounting Windows Images");

                // Show summary
                var successCount = mountedImages.Count(m => m.Status == MountStatus.Mounted);
                var failCount = mountedImages.Count(m => m.Status == MountStatus.Failed);
                
                LoggingService.WriteVerbose(this, $"Mount operation complete: {successCount} successful, {failCount} failed");

                // Output results
                WriteObject(mountedImages.ToArray());

                var duration = DateTime.UtcNow - startTime;
                LoggingService.LogOperationComplete(this, "MountImageList", duration, $"Mounted {successCount} of {imageInfoList.Count} images");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, "Failed to mount images", ex);
                throw;
            }
        }

        /// <summary>
        /// Mounts a single image and returns the mounted image object
        /// </summary>
        private MountedWindowsImage MountSingleImage(WindowsImageInfo imageInfo, string mountRoot, string wimGuid, int currentIndex, int totalCount)
        {
            var mountId = Guid.NewGuid().ToString();
            var mountPath = ConfigurationService.CreateUniqueMountDirectory(mountRoot, imageInfo.Index, wimGuid);
            
            LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Created mount directory: {mountPath}");

            var mountedImage = new MountedWindowsImage
            {
                MountId = mountId,
                SourceImagePath = imageInfo.SourcePath ?? ImagePath.FullName,
                ImageIndex = imageInfo.Index,
                ImageName = imageInfo.Name,
                Edition = imageInfo.Edition,
                Architecture = imageInfo.Architecture,
                MountPath = mountPath,
                WimGuid = wimGuid,
                Status = MountStatus.Mounting,
                IsReadOnly = !ReadWrite.IsPresent,
                ImageSize = imageInfo.Size
            };

            try
            {
                LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Mounting image {imageInfo.Index} to {mountPath}");

                // Report mount progress - start
                LoggingService.WriteProgress(this, "Mounting Windows Images",
                    $"[{currentIndex} of {totalCount}] - {imageInfo.Name}",
                    $"Initiating mount operation for image {imageInfo.Index}...",
                    (int)((double)(currentIndex - 1) / totalCount * 100) + 10);

                var mountStartTime = DateTime.UtcNow;
                Microsoft.Dism.DismApi.MountImage(ImagePath.FullName, mountPath, imageInfo.Index, readOnly: !ReadWrite.IsPresent);
                var mountDuration = DateTime.UtcNow - mountStartTime;

                mountedImage.Status = MountStatus.Mounted;

                // Report mount progress - complete
                LoggingService.WriteProgress(this, "Mounting Windows Images",
                    $"[{currentIndex} of {totalCount}] - {imageInfo.Name}",
                    $"Mount completed in {LoggingService.FormatDuration(mountDuration)}",
                    (int)((double)currentIndex / totalCount * 100));

                LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Image mounted successfully: {imageInfo.Name} (Duration: {LoggingService.FormatDuration(mountDuration)})");
                
                return mountedImage;
            }
            catch (Exception ex)
            {
                mountedImage.Status = MountStatus.Failed;
                mountedImage.ErrorMessage = ex.Message;
                
                // Clean up mount directory if mount failed
                try
                {
                    if (Directory.Exists(mountPath))
                    {
                        Directory.Delete(mountPath, true);
                        LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Cleaned up failed mount directory: {mountPath}");
                    }
                }
                catch (Exception cleanupEx)
                {
                    LoggingService.WriteWarning(this, $"Failed to clean up mount directory {mountPath}: {cleanupEx.Message}");
                }
                
                throw;
            }
        }
    }
}
