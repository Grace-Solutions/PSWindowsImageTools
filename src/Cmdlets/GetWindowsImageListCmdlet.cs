using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security.Cryptography;
using System.Text.Json;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Cmdlet for getting Windows image information from ISO/WIM/ESD files
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "WindowsImageList")]
    [OutputType(typeof(WindowsImageInfo[]))]
    public class GetWindowsImageListCmdlet : PSCmdlet
    {
        /// <summary>
        /// Path to the image file (ISO, WIM, or ESD)
        /// </summary>
        [Parameter(
            Position = 0,
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Path to the image file (ISO, WIM, or ESD)")]
        [ValidateNotNullOrEmpty]
        public FileInfo ImagePath { get; set; } = null!;

        /// <summary>
        /// Enables advanced metadata collection by mounting images (slower but more detailed)
        /// </summary>
        [Parameter(
            HelpMessage = "Enables advanced metadata collection by mounting images (slower but more detailed)")]
        public SwitchParameter IncludeAdvanced { get; set; }

        /// <summary>
        /// Calculate SHA256 hash of the source image file (slower but provides integrity verification)
        /// </summary>
        [Parameter(
            HelpMessage = "Calculate SHA256 hash of the source image file (slower but provides integrity verification)")]
        public SwitchParameter IncludeHash { get; set; }

        /// <summary>
        /// Inclusion filter scriptblock to select which images to process (e.g., {$_.Name -like "*Pro*"} or {$_.Index -eq 1})
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNull]
        public ScriptBlock? InclusionFilter { get; set; }

        /// <summary>
        /// Exclusion filter scriptblock to exclude images from processing (e.g., {$_.Name -like "*Enterprise*"})
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNull]
        public ScriptBlock? ExclusionFilter { get; set; }

        /// <summary>
        /// Skip dismounting images after processing (keeps them mounted for use with other cmdlets)
        /// </summary>
        [Parameter(
            HelpMessage = "Skip dismounting images after processing (keeps them mounted for use with other cmdlets)")]
        public SwitchParameter SkipDismount { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            var startTime = DateTime.UtcNow;
            var imageInfoList = new List<WindowsImageInfo>();

            try
            {


                // Validate input file
                if (!ImagePath.Exists)
                {
                    var errorMessage = $"Image file not found: {ImagePath.FullName}";
                    LoggingService.WriteError(this, "GetImageList", errorMessage);
                    ThrowTerminatingError(new ErrorRecord(
                        new FileNotFoundException(errorMessage),
                        "ImageFileNotFound",
                        ErrorCategory.ObjectNotFound,
                        ImagePath.FullName));
                    return;
                }

                // Use default mount root directory
                var mountRoot = ConfigurationService.DefaultMountRootDirectory;
                LoggingService.WriteVerbose(this, $"Using mount root directory: {mountRoot}");

                // Validate mount root directory
                if (!ConfigurationService.ValidateMountRootDirectory(mountRoot))
                {
                    var errorMessage = $"Cannot access or create mount root directory: {mountRoot}";
                    LoggingService.WriteError(this, "GetImageList", errorMessage);
                    ThrowTerminatingError(new ErrorRecord(
                        new DirectoryNotFoundException(errorMessage),
                        "MountRootDirectoryInvalid",
                        ErrorCategory.InvalidArgument,
                        mountRoot));
                    return;
                }

                // Clean up old mount directories
                ConfigurationService.CleanupMountDirectories(mountRoot);

                // Determine the actual image file to process
                var imageFilePath = GetImageFilePath(ImagePath.FullName);
                LoggingService.WriteVerbose(this, $"Processing image file: {imageFilePath}");

                // Get basic image information using DISM
                using (var dismService = new DismService())
                {
                    imageInfoList = dismService.GetImageInfo(imageFilePath, this);
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

                    // Show initial progress for all images found
                    LoggingService.WriteProgress(this, "Processing Windows Images",
                        $"Processing {imageInfoList.Count} images from {ImagePath.Name}",
                        $"Selected images: {string.Join(", ", imageInfoList.Select(img => $"Index {img.Index}"))}");

                    // Generate one GUID per WIM file for mount organization
                    var wimGuid = Guid.NewGuid().ToString();

                    // Get advanced information if requested
                    if (IncludeAdvanced.IsPresent)
                    {
                        LoggingService.WriteVerbose(this, "Advanced metadata requested, mounting images...");

                        for (int i = 0; i < imageInfoList.Count; i++)
                        {
                            var imageInfo = imageInfoList[i];
                            var progress = (int)((double)(i + 1) / imageInfoList.Count * 100);

                            LoggingService.WriteProgress(this, "Processing Windows Images",
                                $"[{i + 1} of {imageInfoList.Count}] - {imageInfo.Name}",
                                $"Processing Image Index {imageInfo.Index} ({progress}%)", progress);

                            try
                            {
                                var mountDir = ConfigurationService.CreateUniqueMountDirectory(mountRoot, imageInfo.Index, wimGuid);
                                LoggingService.WriteVerbose(this, $"[{i + 1} of {imageInfoList.Count}] - Created mount directory: {mountDir}");

                                try
                                {
                                    var (advancedInfo, mountedImage) = dismService.GetAdvancedImageInfo(imageFilePath, imageInfo.Index, mountDir, this, SkipDismount.IsPresent);
                                    imageInfo.AdvancedInfo = advancedInfo;
                                    imageInfo.MountedImage = mountedImage;

                                    if (mountedImage != null)
                                    {
                                        // Update mounted image with additional info from imageInfo
                                        mountedImage.ImageName = imageInfo.Name;
                                        mountedImage.Edition = imageInfo.Edition;
                                        mountedImage.Architecture = imageInfo.Architecture;
                                        mountedImage.ImageSize = imageInfo.Size;
                                    }

                                    LoggingService.WriteVerbose(this, $"[{i + 1} of {imageInfoList.Count}] - Advanced information collected for image {imageInfo.Index}: {imageInfo.Name}");
                                }
                                finally
                                {
                                    // Clean up mount directory
                                    try
                                    {
                                        if (Directory.Exists(mountDir))
                                        {
                                            Directory.Delete(mountDir, true);
                                            LoggingService.WriteVerbose(this, $"[{i + 1} of {imageInfoList.Count}] - Cleaned up mount directory: {mountDir}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LoggingService.WriteWarning(this, $"Failed to clean up mount directory {mountDir}: {ex.Message}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LoggingService.WriteWarning(this, $"Failed to get advanced info for image {imageInfo.Index}: {ex.Message}");
                            }
                        }

                        LoggingService.CompleteProgress(this, "Processing Windows Images");
                    }
                    else
                    {
                        // Complete progress for basic image discovery
                        LoggingService.CompleteProgress(this, "Processing Windows Images");
                    }
                }

                // Calculate hash if requested (independent of database)
                if (IncludeHash.IsPresent && imageInfoList.Count > 0 && !string.IsNullOrEmpty(imageInfoList[0].SourcePath))
                {
                    LoggingService.WriteVerbose(this, "Calculating SHA256 hash for source file...");
                    var sourceHash = CalculateFileHash(imageInfoList[0].SourcePath);
                    LoggingService.WriteVerbose(this, $"SHA256 hash calculation completed: {sourceHash}");

                    // Set hash on all images from the same source file
                    foreach (var imageInfo in imageInfoList)
                    {
                        imageInfo.SourceHash = sourceHash;
                    }
                }



                var duration = DateTime.UtcNow - startTime;
                LoggingService.LogOperationComplete(this, "GetImageList", duration, 
                    $"Processed {imageInfoList.Count} images from {ImagePath.FullName}");



                // Output results
                WriteObject(imageInfoList.ToArray());
            }
            catch (Exception ex)
            {
                LoggingService.LogOperationFailure(this, "GetImageList", ex);
                ThrowTerminatingError(new ErrorRecord(
                    ex,
                    "GetImageListFailed",
                    ErrorCategory.NotSpecified,
                    ImagePath.FullName));
            }
        }

        /// <summary>
        /// Determines the actual image file path, handling ISO files
        /// </summary>
        /// <param name="inputPath">Input file path</param>
        /// <returns>Path to the WIM/ESD file to process</returns>
        private string GetImageFilePath(string inputPath)
        {
            var extension = Path.GetExtension(inputPath).ToLowerInvariant();
            
            if (extension == ".iso")
            {
                // For ISO files, we need to find the install.wim or install.esd inside
                // This is a simplified implementation - in reality, you'd mount the ISO
                LoggingService.WriteVerbose(this, "ISO file detected - this implementation requires the ISO to be already extracted or mounted");
                throw new NotImplementedException("ISO file processing is not yet implemented. Please extract the ISO and point to the install.wim or install.esd file directly.");
            }
            
            return inputPath;
        }



        /// <summary>
        /// Calculates SHA256 hash of a file
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>SHA256 hash as hex string</returns>
        private static string CalculateFileHash(string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hashBytes = sha256.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
