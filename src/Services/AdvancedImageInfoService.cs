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
        /// Gets essential Windows version registry information from an image by mounting it
        /// Only reads the Windows CurrentVersion registry key for version information
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="imageIndex">Index of the image to mount</param>
        /// <param name="mountPath">Path where to mount the image</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>Advanced image information</returns>
        public WindowsImageAdvancedInfo GetAdvancedImageInfo(string imagePath, int imageIndex, string mountPath, PSCmdlet cmdlet)
        {
            Initialize();
            var advancedInfo = new WindowsImageAdvancedInfo();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Getting Windows version info for image {imageIndex}");

                // Mount the image
                DismApi.MountImage(imagePath, mountPath, imageIndex);

                try
                {
                    // Only read essential Windows version registry information
                    using var registryService = new OfflineRegistryService();
                    advancedInfo.RegistryInfo = registryService.ReadWindowsVersionInfo(mountPath, cmdlet);
                }
                finally
                {
                    // Always unmount the image
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
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    $"Failed to get Windows version information: {ex.Message}", ex);
                throw;
            }

            return advancedInfo;
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
