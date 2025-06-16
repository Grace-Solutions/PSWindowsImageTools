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
        /// Gets comprehensive advanced information about an image by mounting it
        /// Uses specialized services for different types of operations
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
                    $"Starting advanced info gathering for image {imageIndex} from {imagePath}");
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    "Using specialized services: DISM API for features/packages/drivers, Registry API for offline registry");

                // Mount the image
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Mounting image to {mountPath}");
                DismApi.MountImage(imagePath, mountPath, imageIndex);

                try
                {
                    // === DISM-BASED OPERATIONS ===
                    // These use the DISM API and require a mounted image with DISM session
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Phase 1: Gathering DISM-based information");
                    
                    using (var session = DismApi.OpenOfflineSession(mountPath))
                    {
                        // Get Windows Features
                        var featureInfo = GetWindowsFeatures(session, cmdlet);
                        advancedInfo.InstalledFeatures = featureInfo.InstalledFeatures;
                        advancedInfo.AvailableFeatures = featureInfo.AvailableFeatures;

                        // Get Installed Packages
                        advancedInfo.InstalledPackages = GetInstalledPackages(session, cmdlet);

                        // Get Installed Drivers
                        advancedInfo.InstalledDrivers = GetInstalledDrivers(session, cmdlet);
                    }

                    // === REGISTRY-BASED OPERATIONS ===
                    // These use the Registry API and work directly with hive files
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Phase 2: Gathering registry-based information");
                    
                    using var registryService = new OfflineRegistryService();
                    advancedInfo.RegistryInfo = registryService.ReadOfflineRegistryInfo(mountPath, cmdlet);

                    // === FILE SYSTEM-BASED OPERATIONS ===
                    // These work directly with the mounted file system
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Phase 3: Gathering file system-based information");
                    
                    var fileSystemInfo = GetFileSystemInfo(mountPath, cmdlet);
                    foreach (var item in fileSystemInfo)
                    {
                        advancedInfo.RegistryInfo[item.Key] = item.Value;
                    }

                    LoggingService.WriteVerbose(cmdlet, ServiceName, 
                        "Advanced information gathering completed successfully using specialized services");
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
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, 
                    $"Failed to get advanced image information: {ex.Message}", ex);
                throw;
            }

            return advancedInfo;
        }

        /// <summary>
        /// Gets Windows features information using DISM API
        /// </summary>
        private (System.Collections.Generic.List<string> InstalledFeatures, System.Collections.Generic.List<string> AvailableFeatures) 
            GetWindowsFeatures(DismSession session, PSCmdlet cmdlet)
        {
            var installedFeatures = new System.Collections.Generic.List<string>();
            var availableFeatures = new System.Collections.Generic.List<string>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, "Reading Windows features using DISM API");
                
                var features = DismApi.GetFeatures(session);

                installedFeatures = features
                    .Where(f => f.State == DismPackageFeatureState.Installed)
                    .Select(f => f.FeatureName)
                    .ToList();

                availableFeatures = features
                    .Where(f => f.State == DismPackageFeatureState.Removed)
                    .Select(f => f.FeatureName)
                    .ToList();

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Found {installedFeatures.Count} installed and {availableFeatures.Count} available features");
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to get features: {ex.Message}");
            }

            return (installedFeatures, availableFeatures);
        }

        /// <summary>
        /// Gets installed packages information using DISM API
        /// </summary>
        private System.Collections.Generic.List<string> GetInstalledPackages(DismSession session, PSCmdlet cmdlet)
        {
            var installedPackages = new System.Collections.Generic.List<string>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, "Reading installed packages using DISM API");
                
                var packages = DismApi.GetPackages(session);

                installedPackages = packages
                    .Where(p => p.PackageState == DismPackageFeatureState.Installed)
                    .Select(p => p.PackageName)
                    .ToList();

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Found {installedPackages.Count} installed packages");
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to get packages: {ex.Message}");
            }

            return installedPackages;
        }

        /// <summary>
        /// Gets installed drivers information using DISM API
        /// </summary>
        private System.Collections.Generic.List<string> GetInstalledDrivers(DismSession session, PSCmdlet cmdlet)
        {
            var installedDrivers = new System.Collections.Generic.List<string>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, "Reading installed drivers using DISM API");
                
                var drivers = DismApi.GetDrivers(session, false);

                installedDrivers = drivers
                    .Select(d => $"{d.ProviderName} - {d.DriverSignature}")
                    .ToList();

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Found {installedDrivers.Count} installed drivers");
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to get drivers: {ex.Message}");
            }

            return installedDrivers;
        }

        /// <summary>
        /// Gets file system-based information from mounted image
        /// </summary>
        private System.Collections.Generic.Dictionary<string, object> GetFileSystemInfo(string mountPath, PSCmdlet cmdlet)
        {
            var fileSystemInfo = new System.Collections.Generic.Dictionary<string, object>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, "Reading file system information");

                // Check for important directories and files
                var windowsPath = Path.Combine(mountPath, "Windows");
                var system32Path = Path.Combine(windowsPath, "System32");
                var programFilesPath = Path.Combine(mountPath, "Program Files");
                var programFilesX86Path = Path.Combine(mountPath, "Program Files (x86)");

                fileSystemInfo["WindowsDirectoryExists"] = Directory.Exists(windowsPath);
                fileSystemInfo["System32DirectoryExists"] = Directory.Exists(system32Path);
                fileSystemInfo["ProgramFilesDirectoryExists"] = Directory.Exists(programFilesPath);
                fileSystemInfo["ProgramFilesX86DirectoryExists"] = Directory.Exists(programFilesX86Path);

                // Get directory sizes (basic info)
                if (Directory.Exists(windowsPath))
                {
                    var windowsDir = new DirectoryInfo(windowsPath);
                    fileSystemInfo["WindowsDirectoryFileCount"] = windowsDir.GetFiles("*", SearchOption.TopDirectoryOnly).Length;
                    fileSystemInfo["WindowsDirectorySubdirCount"] = windowsDir.GetDirectories().Length;
                }

                // Check for specific important files
                var importantFiles = new[]
                {
                    Path.Combine(system32Path, "ntoskrnl.exe"),
                    Path.Combine(system32Path, "kernel32.dll"),
                    Path.Combine(system32Path, "user32.dll"),
                    Path.Combine(windowsPath, "explorer.exe")
                };

                foreach (var file in importantFiles)
                {
                    var fileName = Path.GetFileName(file);
                    fileSystemInfo[$"{fileName}Exists"] = File.Exists(file);
                    
                    if (File.Exists(file))
                    {
                        var fileInfo = new FileInfo(file);
                        fileSystemInfo[$"{fileName}Size"] = fileInfo.Length;
                        fileSystemInfo[$"{fileName}LastModified"] = fileInfo.LastWriteTime;
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Collected {fileSystemInfo.Count} file system information items");
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to get file system info: {ex.Message}");
                fileSystemInfo["FileSystemInfoError"] = ex.Message;
            }

            return fileSystemInfo;
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
