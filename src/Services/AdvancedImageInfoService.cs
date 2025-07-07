using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Microsoft.Dism;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Dedicated service for gathering advanced image information
    /// Orchestrates different specialized services for different types of operations
    /// Uses native DISM API for real progress callbacks during mount operations
    /// </summary>
    public class AdvancedImageInfoService : IDisposable
    {
        private const string ServiceName = "AdvancedImageInfoService";
        private bool _disposed = false;
        private readonly NativeDismService _nativeDismService;

        /// <summary>
        /// Initializes a new instance of the AdvancedImageInfoService
        /// </summary>
        public AdvancedImageInfoService()
        {
            _nativeDismService = new NativeDismService();
        }

        /// <summary>
        /// Gets advanced registry information from an image by mounting it
        /// Reads Windows version info, installed software, and Windows Update configuration
        /// Uses native DISM API with real progress callbacks for accurate mount progress
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="imageIndex">Index of the image to mount</param>
        /// <param name="mountPath">Path where to mount the image</param>
        /// <param name="cmdlet">Cmdlet for logging and progress reporting</param>
        /// <param name="skipDismount">If true, keeps the image mounted and returns mount info</param>
        /// <param name="progressCallback">Optional progress callback for mount operation</param>
        /// <returns>Tuple containing advanced image information and optional mounted image info</returns>
        public (WindowsImageAdvancedInfo AdvancedInfo, MountedWindowsImage? MountedImage) GetAdvancedImageInfo(string imagePath, int imageIndex, string mountPath, PSCmdlet cmdlet, bool skipDismount = false, Action<int, string>? progressCallback = null)
        {
            var advancedInfo = new WindowsImageAdvancedInfo();
            MountedWindowsImage? mountedImage = null;

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Getting advanced registry info for image {imageIndex} using native DISM API");

                // Mount the image using native DISM API with progress callbacks
                var mountSuccess = _nativeDismService.MountImage(
                    imagePath,
                    mountPath,
                    (uint)imageIndex,
                    readOnly: true,
                    progressCallback: progressCallback,
                    cmdlet: cmdlet);

                if (!mountSuccess)
                {
                    throw new InvalidOperationException($"Failed to mount image {imageIndex} from {imagePath}");
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Image {imageIndex} successfully mounted to {mountPath} using native DISM API");

                // Create mounted image info if we're keeping it mounted
                if (skipDismount)
                {
                    mountedImage = new MountedWindowsImage
                    {
                        MountId = Guid.NewGuid().ToString(),
                        SourceImagePath = imagePath,
                        ImageIndex = imageIndex,
                        MountPath = new DirectoryInfo(mountPath),
                        Status = MountStatus.Mounted,
                        IsReadOnly = true,
                        MountedAt = DateTime.UtcNow
                    };

                    LoggingService.WriteVerbose(cmdlet, ServiceName,
                        $"Image will remain mounted for use with other cmdlets (MountId: {mountedImage.MountId})");
                }

                try
                {
                    // Read complete registry information using RegistryPackageService
                    using var registryService = new OfflineRegistryService();
                    advancedInfo.RegistryInfo = registryService.ReadOfflineRegistryInfo(mountPath, cmdlet);
                }
                finally
                {
                    // Only unmount if not skipping dismount
                    if (!skipDismount)
                    {
                        try
                        {
                            // Use native DISM API for unmounting as well
                            var unmountSuccess = _nativeDismService.UnmountImage(mountPath, false, cmdlet: cmdlet);
                            if (!unmountSuccess)
                            {
                                LoggingService.WriteWarning(cmdlet, ServiceName, "Failed to unmount image using native API");
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to unmount image: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    $"Failed to get advanced registry information: {ex.Message}", ex);
                throw;
            }

            return (advancedInfo, mountedImage);
        }



        /// <summary>
        /// Disposes the advanced image info service
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _nativeDismService?.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
