using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Microsoft.Dism;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Dedicated service for gathering advanced image information from mounted images
    /// Orchestrates different specialized services for reading registry and system information
    /// Does NOT handle mounting - expects images to already be mounted
    /// </summary>
    public class AdvancedImageInfoService : IDisposable
    {
        private const string ServiceName = "AdvancedImageInfoService";
        private bool _disposed = false;

        /// <summary>
        /// Gets advanced registry information from an already-mounted image
        /// Reads Windows version info, installed software, and Windows Update configuration
        /// Does NOT handle mounting/unmounting - expects the image to already be mounted at mountPath
        /// </summary>
        /// <param name="mountPath">Path where the image is already mounted</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>Advanced image information collected from the mounted image</returns>
        public WindowsImageAdvancedInfo GetAdvancedImageInfo(string mountPath, PSCmdlet cmdlet)
        {
            var advancedInfo = new WindowsImageAdvancedInfo();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Reading advanced registry information from mounted image at: {mountPath}");

                // Verify the mount path exists and contains a mounted Windows image
                if (!Directory.Exists(mountPath))
                {
                    throw new DirectoryNotFoundException($"Mount path does not exist: {mountPath}");
                }

                var windowsDir = Path.Combine(mountPath, "Windows");
                if (!Directory.Exists(windowsDir))
                {
                    throw new InvalidOperationException($"No Windows directory found at mount path. Ensure the image is properly mounted at: {mountPath}");
                }

                // Read complete registry information using OfflineRegistryService
                using var registryService = new OfflineRegistryService();
                advancedInfo.RegistryInfo = registryService.ReadOfflineRegistryInfo(mountPath, cmdlet);

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Successfully collected advanced registry information from mounted image");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    $"Failed to get advanced registry information: {ex.Message}", ex);
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
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
