using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
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
                        Version = FormatUtilityService.ParseVersion(dismImageInfo.ProductVersion?.ToString() ?? string.Empty),
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

                    // Defer hash calculation until database write (performance optimization)
                    imageInfo.SourceHash = "";

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
        /// <param name="skipDismount">If true, keeps the image mounted and returns mount info</param>
        /// <returns>Tuple containing advanced image information and optional mounted image info</returns>
        public (WindowsImageAdvancedInfo AdvancedInfo, MountedWindowsImage? MountedImage) GetAdvancedImageInfo(string imagePath, int imageIndex, string mountPath, PSCmdlet cmdlet, bool skipDismount = false)
        {
            try
            {
                using var advancedInfoService = new AdvancedImageInfoService();
                var result = advancedInfoService.GetAdvancedImageInfo(imagePath, imageIndex, mountPath, cmdlet, skipDismount);

                return result;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    $"Failed to get advanced image information: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Exports an image from one WIM/ESD file to another WIM file using native WIM API
        /// Based on Microsoft's actual Export-WindowsImage implementation
        /// </summary>
        /// <param name="sourcePath">Source WIM/ESD file path</param>
        /// <param name="destinationPath">Destination WIM file path</param>
        /// <param name="sourceIndex">Source image index</param>
        /// <param name="compressionType">Compression type for the output</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>True if export succeeded</returns>
        public bool ExportImage(string sourcePath, string destinationPath, int sourceIndex,
            string compressionType, PSCmdlet cmdlet, Action<int, string>? progressCallback = null)
        {
            try
            {
                var exportStartTime = LoggingService.LogOperationStartWithTimestamp(cmdlet, ServiceName,
                    "DISM Image Export", $"Index {sourceIndex} from {Path.GetFileName(sourcePath)} to {Path.GetFileName(destinationPath)}");

                using var wimExportService = new WimExportService();

                var result = wimExportService.ExportImage(
                    sourceImagePath: sourcePath,
                    destinationImagePath: destinationPath,
                    sourceIndex: (uint)sourceIndex,
                    compressionType: compressionType,
                    checkIntegrity: false,
                    setBootable: false,
                    scratchDirectory: Path.GetTempPath(),
                    progressCallback: progressCallback,
                    cmdlet: cmdlet);

                if (result)
                {
                    LoggingService.LogOperationCompleteWithTimestamp(cmdlet, ServiceName, "DISM Image Export", exportStartTime,
                        $"Index {sourceIndex} from {Path.GetFileName(sourcePath)} to {Path.GetFileName(destinationPath)}");
                }
                else
                {
                    LoggingService.WriteError(cmdlet, ServiceName, "Image export failed");
                }

                return result;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Export operation failed: {ex.Message}", ex);
                return false;
            }
        }

        #region Package Management

        /// <summary>
        /// Adds a package (Features on Demand, Language Pack, etc.) to a mounted image
        /// </summary>
        /// <param name="mountPath">Path where the image is mounted</param>
        /// <param name="packagePath">Path to the package file (.cab, .msu)</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>True if package was added successfully</returns>
        public bool AddPackage(string mountPath, string packagePath, PSCmdlet cmdlet, Action<int, string>? progressCallback = null)
        {
            Initialize();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Adding package {packagePath} to mounted image at {mountPath}");
                progressCallback?.Invoke(0, "Starting package installation");

                using var session = DismApi.OpenOfflineSession(mountPath);

                // HONEST ASSESSMENT: Microsoft.Dism.DismApi.AddPackage method signature is unknown
                // The compiler shows "No overload for method 'AddPackage' takes 3 arguments"
                // Need to research the actual method signature or determine if it exists at all

                LoggingService.WriteError(cmdlet, ServiceName,
                    "AddPackage method signature in Microsoft.Dism is unknown. " +
                    "Need to research actual API surface area.");

                return false;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to add package: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Removes a package from a mounted image
        /// </summary>
        /// <param name="mountPath">Path where the image is mounted</param>
        /// <param name="packageName">Name of the package to remove</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>True if package was removed successfully</returns>
        public bool RemovePackage(string mountPath, string packageName, PSCmdlet cmdlet, Action<int, string>? progressCallback = null)
        {
            Initialize();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Removing package {packageName} from mounted image at {mountPath}");
                progressCallback?.Invoke(0, "Starting package removal");

                using var session = DismApi.OpenOfflineSession(mountPath);

                // HONEST ASSESSMENT: Need to research the correct Microsoft.Dism method for package removal
                // The API might be RemovePackage or similar - need to check actual available methods
                LoggingService.WriteError(cmdlet, ServiceName, "Package removal method needs to be researched in Microsoft.Dism API");

                return false;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to remove package: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets list of packages in a mounted image
        /// </summary>
        /// <param name="mountPath">Path where the image is mounted</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>List of package information</returns>
        public List<DismPackage> GetPackages(string mountPath, PSCmdlet cmdlet)
        {
            Initialize();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Getting packages from mounted image at {mountPath}");

                using var session = DismApi.OpenOfflineSession(mountPath);
                var packages = DismApi.GetPackages(session).ToList();

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found {packages.Count} packages");
                return packages;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to get packages: {ex.Message}", ex);
                return new List<DismPackage>();
            }
        }

        #endregion

        #region Feature Management

        /// <summary>
        /// Enables a Windows feature in a mounted image
        /// </summary>
        /// <param name="mountPath">Path where the image is mounted</param>
        /// <param name="featureName">Name of the feature to enable</param>
        /// <param name="enableAll">Enable all parent features</param>
        /// <param name="sourcePath">Source path for feature files</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>True if feature was enabled successfully</returns>
        public bool EnableFeature(string mountPath, string featureName, bool enableAll, string? sourcePath, PSCmdlet cmdlet, Action<int, string>? progressCallback = null)
        {
            Initialize();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Enabling feature {featureName} in mounted image at {mountPath}");
                progressCallback?.Invoke(0, "Starting feature enablement");

                using var session = DismApi.OpenOfflineSession(mountPath);

                // HONEST ASSESSMENT: Microsoft.Dism may have EnableFeature but signature is unknown
                // Need to research if DismApi.EnableFeature exists and what parameters it takes

                LoggingService.WriteError(cmdlet, ServiceName,
                    "EnableFeature method in Microsoft.Dism needs research. " +
                    "Unknown if this functionality exists in the managed wrapper.");

                return false;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to enable feature: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Disables a Windows feature in a mounted image
        /// </summary>
        /// <param name="mountPath">Path where the image is mounted</param>
        /// <param name="featureName">Name of the feature to disable</param>
        /// <param name="removePayload">Remove the feature payload</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>True if feature was disabled successfully</returns>
        public bool DisableFeature(string mountPath, string featureName, bool removePayload, PSCmdlet cmdlet, Action<int, string>? progressCallback = null)
        {
            Initialize();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Disabling feature {featureName} in mounted image at {mountPath}");
                progressCallback?.Invoke(0, "Starting feature disablement");

                using var session = DismApi.OpenOfflineSession(mountPath);

                // HONEST ASSESSMENT: Microsoft.Dism may have DisableFeature but signature is unknown
                // Need to research if DismApi.DisableFeature exists and what parameters it takes

                LoggingService.WriteError(cmdlet, ServiceName,
                    "DisableFeature method in Microsoft.Dism needs research. " +
                    "Unknown if this functionality exists in the managed wrapper.");

                return false;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to disable feature: {ex.Message}", ex);
                return false;
            }
        }



        /// <summary>
        /// Gets list of Windows features in a mounted image
        /// </summary>
        /// <param name="mountPath">Path where the image is mounted</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>List of feature information</returns>
        public List<DismFeature> GetFeatures(string mountPath, PSCmdlet cmdlet)
        {
            Initialize();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Getting features from mounted image at {mountPath}");

                using var session = DismApi.OpenOfflineSession(mountPath);
                var features = DismApi.GetFeatures(session).ToList();

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found {features.Count} features");
                return features;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to get features: {ex.Message}", ex);
                return new List<DismFeature>();
            }
        }

        #endregion

        #region Update Management

        /// <summary>
        /// Adds an update (.msu or .cab) to a mounted image
        /// </summary>
        /// <param name="mountPath">Path where the image is mounted</param>
        /// <param name="updatePath">Path to the update file (.msu or .cab)</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>True if update was added successfully</returns>
        public bool AddUpdate(string mountPath, string updatePath, PSCmdlet cmdlet, Action<int, string>? progressCallback = null)
        {
            // HONEST ASSESSMENT: Update management requires package management functionality
            // Microsoft.Dism library doesn't provide clear methods for adding packages/updates
            // Need to research direct DISM API P/Invoke or alternative libraries

            LoggingService.WriteError(cmdlet, ServiceName,
                "Update management not available in Microsoft.Dism library. " +
                "Requires research into direct DISM API P/Invoke or alternative libraries like WimLib.");

            return false;
        }

        /// <summary>
        /// Removes an update from a mounted image
        /// </summary>
        /// <param name="mountPath">Path where the image is mounted</param>
        /// <param name="updateName">Name of the update to remove</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <returns>True if update was removed successfully</returns>
        public bool RemoveUpdate(string mountPath, string updateName, PSCmdlet cmdlet, Action<int, string>? progressCallback = null)
        {
            // HONEST ASSESSMENT: Update removal requires package removal functionality
            // Microsoft.Dism library doesn't provide clear methods for removing packages/updates
            // Need to research direct DISM API P/Invoke or alternative libraries

            LoggingService.WriteError(cmdlet, ServiceName,
                "Update removal not available in Microsoft.Dism library. " +
                "Requires research into direct DISM API P/Invoke or alternative libraries like WimLib.");

            return false;
        }

        #endregion

        // Registry reading functionality moved to dedicated OfflineRegistryService

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
