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
    /// </summary>
    public class AdvancedImageInfoService : IDisposable
    {
        private const string ServiceName = "AdvancedImageInfoService";
        private bool _disposed = false;
        private bool _dismInitialized = false;

        /// <summary>
        /// Initializes the DISM API if not already initialized
        /// </summary>
        private void Initialize()
        {
            if (!_dismInitialized)
            {
                DismApi.Initialize(DismLogLevel.LogErrors);
                _dismInitialized = true;
            }
        }

        /// <summary>
        /// Gets advanced registry information from an image by mounting it
        /// Reads Windows version info, installed software, and Windows Update configuration
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="imageIndex">Index of the image to mount</param>
        /// <param name="mountPath">Path where to mount the image</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <param name="skipDismount">If true, keeps the image mounted and returns mount info</param>
        /// <returns>Tuple containing advanced image information and optional mounted image info</returns>
        public (WindowsImageAdvancedInfo AdvancedInfo, MountedWindowsImage? MountedImage) GetAdvancedImageInfo(string imagePath, int imageIndex, string mountPath, PSCmdlet cmdlet, bool skipDismount = false)
        {
            Initialize();
            var advancedInfo = new WindowsImageAdvancedInfo();
            MountedWindowsImage? mountedImage = null;

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Getting advanced registry info for image {imageIndex}");

                // Mount the image
                DismApi.MountImage(imagePath, mountPath, imageIndex);

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Image {imageIndex} successfully mounted to {mountPath}");

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
                            DismApi.UnmountImage(mountPath, false); // Discard changes
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
                if (_dismInitialized)
                {
                    try
                    {
                        DismApi.Shutdown();
                    }
                    catch
                    {
                        // Ignore shutdown errors
                    }
                    _dismInitialized = false;
                }
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
