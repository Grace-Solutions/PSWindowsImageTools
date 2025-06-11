using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Microsoft.Dism;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for DISM operations on Windows images using Microsoft.Dism (ManagedDism)
    /// </summary>
    public class DismService : IDisposable
    {
        private const string ServiceName = "DismService";
        private bool _dismInitialized = false;
        private bool _disposed = false;

        /// <summary>
        /// Initializes the DISM API
        /// </summary>
        public void Initialize()
        {
            if (!_dismInitialized)
            {
                DismApi.Initialize(DismLogLevel.LogErrors);
                _dismInitialized = true;
            }
        }

        /// <summary>
        /// Gets information about all images in a WIM/ESD file
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>List of WindowsImageInfo objects</returns>
        public List<WindowsImageInfo> GetImageInfo(string imagePath, PSCmdlet cmdlet)
        {
            Initialize();
            var imageInfoList = new List<WindowsImageInfo>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Getting image information from: {imagePath}");

                var imageInfoCollection = DismApi.GetImageInfo(imagePath);

                foreach (var dismImageInfo in imageInfoCollection)
                {
                    var imageInfo = new WindowsImageInfo
                    {
                        Index = (int)dismImageInfo.ImageIndex,
                        Name = dismImageInfo.ImageName ?? string.Empty,
                        Description = dismImageInfo.ImageDescription ?? string.Empty,
                        Size = (long)dismImageInfo.ImageSize,
                        Architecture = dismImageInfo.Architecture.ToString(),
                        ProductType = dismImageInfo.ProductType ?? string.Empty,
                        InstallationType = dismImageInfo.InstallationType ?? string.Empty,
                        Edition = dismImageInfo.EditionId ?? string.Empty,
                        Version = dismImageInfo.ProductVersion?.ToString() ?? string.Empty,
                        Build = dismImageInfo.ProductVersion?.Build.ToString() ?? string.Empty,
                        ServicePackLevel = dismImageInfo.SpLevel.ToString(),
                        DefaultLanguage = dismImageInfo.DefaultLanguage?.Name ?? string.Empty,
                        Languages = dismImageInfo.Languages?.Select(l => l.Name).ToList() ?? new List<string>(),
                        CreatedTime = DateTime.UtcNow, // DISM API doesn't provide creation time
                        ModifiedTime = DateTime.UtcNow, // DISM API doesn't provide modification time
                        SourcePath = imagePath,
                        SystemRoot = dismImageInfo.SystemRoot ?? string.Empty,
                        ProductSuite = dismImageInfo.ProductSuite ?? string.Empty
                    };

                    // Calculate source file hash
                    imageInfo.SourceHash = CalculateFileHash(imagePath);

                    imageInfoList.Add(imageInfo);
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found {imageInfoList.Count} images in file");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to get image information: {ex.Message}", ex);
                throw;
            }

            return imageInfoList;
        }

        /// <summary>
        /// Gets advanced information about an image by mounting it
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
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Mounting image {imageIndex} from {imagePath} to {mountPath}");

                // Mount the image first
                DismApi.MountImage(imagePath, mountPath, imageIndex);

                try
                {
                    // Open a session to the mounted image
                    using (var session = DismApi.OpenOfflineSession(mountPath))
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName, "Image mounted successfully, gathering advanced information");

                        // Get installed features
                        try
                        {
                            var features = DismApi.GetFeatures(session);

                            advancedInfo.InstalledFeatures = features
                                .Where(f => f.State == DismPackageFeatureState.Installed)
                                .Select(f => f.FeatureName)
                                .ToList();

                            advancedInfo.AvailableFeatures = features
                                .Where(f => f.State == DismPackageFeatureState.Removed)
                                .Select(f => f.FeatureName)
                                .ToList();

                            LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found {advancedInfo.InstalledFeatures.Count} installed features");
                        }
                        catch (Exception ex)
                        {
                            LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to get features: {ex.Message}");
                        }

                        // Get installed packages
                        try
                        {
                            var packages = DismApi.GetPackages(session);

                            advancedInfo.InstalledPackages = packages
                                .Where(p => p.PackageState == DismPackageFeatureState.Installed)
                                .Select(p => p.PackageName)
                                .ToList();

                            LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found {advancedInfo.InstalledPackages.Count} installed packages");
                        }
                        catch (Exception ex)
                        {
                            LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to get packages: {ex.Message}");
                        }

                        // Get installed drivers
                        try
                        {
                            var drivers = DismApi.GetDrivers(session, false);

                            advancedInfo.InstalledDrivers = drivers
                                .Select(d => $"{d.ProviderName} - {d.DriverSignature}")
                                .ToList();

                            LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found {advancedInfo.InstalledDrivers.Count} installed drivers");
                        }
                        catch (Exception ex)
                        {
                            LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to get drivers: {ex.Message}");
                        }

                        // Read registry information
                        try
                        {
                            var registryInfo = ReadRegistryInformation(mountPath);
                            advancedInfo.RegistryInfo = registryInfo;
                            LoggingService.WriteVerbose(cmdlet, ServiceName, $"Read {registryInfo.Count} registry values");
                        }
                        catch (Exception ex)
                        {
                            LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to read registry: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    // Always unmount the image
                    try
                    {
                        DismApi.UnmountImage(mountPath, false); // Discard changes
                        LoggingService.WriteVerbose(cmdlet, ServiceName, "Image unmounted successfully");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to unmount image: {ex.Message}");
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, "Advanced information gathered successfully");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to get advanced image information: {ex.Message}", ex);
                throw;
            }

            return advancedInfo;
        }

        /// <summary>
        /// Exports an image from ESD to WIM format
        /// Note: This method is a placeholder - ExportImage is not available in Microsoft.Dism
        /// We'll need to implement this using alternative methods or external tools
        /// </summary>
        /// <param name="sourcePath">Source ESD file path</param>
        /// <param name="destinationPath">Destination WIM file path</param>
        /// <param name="sourceIndex">Source image index</param>
        /// <param name="compressionType">Compression type for the output</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        public void ExportImage(string sourcePath, string destinationPath, int sourceIndex,
            string compressionType, PSCmdlet cmdlet)
        {
            Initialize();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Export operation requested: {sourceIndex} from {sourcePath} to {destinationPath}");

                // TODO: Implement image export using alternative methods
                // The Microsoft.Dism library doesn't include ExportImage functionality
                // We'll need to use DISM.exe directly or find another approach

                throw new NotImplementedException("Image export functionality is not yet implemented. " +
                    "The Microsoft.Dism library doesn't include ExportImage. This will be implemented in a future version.");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to export image: {ex.Message}", ex);
                throw;
            }
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
                using (var sha256 = System.Security.Cryptography.SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Reads basic registry information from a mounted image
        /// </summary>
        /// <param name="mountPath">Path where the image is mounted</param>
        /// <returns>Dictionary of registry information</returns>
        private static Dictionary<string, object> ReadRegistryInformation(string mountPath)
        {
            var registryInfo = new Dictionary<string, object>();

            try
            {
                // This is a simplified registry reading - in a full implementation,
                // you would use the Windows Registry API to read from the mounted hives
                var systemHivePath = Path.Combine(mountPath, "Windows", "System32", "config", "SYSTEM");
                var softwareHivePath = Path.Combine(mountPath, "Windows", "System32", "config", "SOFTWARE");

                if (File.Exists(systemHivePath))
                {
                    registryInfo["SystemHiveExists"] = true;
                    registryInfo["SystemHiveSize"] = new FileInfo(systemHivePath).Length;
                }

                if (File.Exists(softwareHivePath))
                {
                    registryInfo["SoftwareHiveExists"] = true;
                    registryInfo["SoftwareHiveSize"] = new FileInfo(softwareHivePath).Length;
                }
            }
            catch
            {
                // Ignore registry reading errors
            }

            return registryInfo;
        }

        /// <summary>
        /// Disposes the DISM service
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
            }
        }
    }
}
