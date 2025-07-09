using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for configuring wallpaper and lockscreen images in mounted Windows images
    /// Follows the proven approach from Invoke-WallpaperConfiguration.ps1
    /// </summary>
    public class WallpaperConfigurationService : IDisposable
    {
        private const string ServiceName = "WallpaperConfigurationService";
        private bool _disposed = false;

        /// <summary>
        /// Result of wallpaper configuration operation
        /// </summary>
        public class ConfigurationResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public List<string> ProcessedFiles { get; set; } = new List<string>();
            public List<string> Warnings { get; set; } = new List<string>();
            public WallpaperConfiguration Configuration { get; set; } = null!;
        }

        /// <summary>
        /// Configures wallpaper and lockscreen for a mounted Windows image
        /// </summary>
        /// <param name="configuration">Wallpaper configuration settings</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Configuration result</returns>
        public ConfigurationResult ConfigureWallpaper(WallpaperConfiguration configuration, PSCmdlet? cmdlet = null)
        {
            var result = new ConfigurationResult
            {
                Configuration = configuration
            };

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, "Starting wallpaper configuration");

                // Validate configuration
                configuration.Validate();

                using var permissionService = new FilePermissionService();
                using var imageService = new ImageResizingService();

                // Create scratch directory for image processing
                if (!configuration.ImageScratchDirectory.Exists)
                {
                    configuration.ImageScratchDirectory.Create();
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Created scratch directory: {configuration.ImageScratchDirectory.FullName}");
                }

                // Process lockscreen image if provided
                if (configuration.LockscreenSourcePath != null)
                {
                    var lockscreenResult = ProcessLockscreenImage(configuration, permissionService, imageService, cmdlet);
                    if (lockscreenResult.Success)
                    {
                        result.ProcessedFiles.Add(lockscreenResult.ExportFile.FullName);
                        LoggingService.WriteVerbose(cmdlet, ServiceName, "Lockscreen image configured successfully");
                    }
                    else
                    {
                        result.Warnings.Add($"Lockscreen configuration failed: {lockscreenResult.ErrorMessage}");
                    }
                }

                // Process wallpaper images
                var wallpaperResults = ProcessWallpaperImages(configuration, permissionService, imageService, cmdlet);
                
                int successCount = 0;
                foreach (var wallpaperResult in wallpaperResults)
                {
                    if (wallpaperResult.Success)
                    {
                        result.ProcessedFiles.Add(wallpaperResult.ExportFile.FullName);
                        successCount++;
                    }
                    else
                    {
                        result.Warnings.Add($"Wallpaper resize failed: {wallpaperResult.ErrorMessage}");
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Wallpaper processing completed: {successCount}/{wallpaperResults.Count} successful");

                // Copy primary wallpaper
                var primaryWallpaperResult = CopyPrimaryWallpaper(configuration, permissionService, imageService, cmdlet);
                if (primaryWallpaperResult.Success)
                {
                    result.ProcessedFiles.Add(primaryWallpaperResult.ExportFile.FullName);
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Primary wallpaper configured successfully");
                }
                else
                {
                    result.Warnings.Add($"Primary wallpaper configuration failed: {primaryWallpaperResult.ErrorMessage}");
                }

                // Copy default wallpapers to target directory
                CopyDefaultWallpapers(configuration, permissionService, wallpaperResults, cmdlet);

                result.Success = true;
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Wallpaper configuration completed successfully. Processed {result.ProcessedFiles.Count} files");

                return result;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Wallpaper configuration failed: {ex.Message}", ex);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Processes lockscreen image
        /// </summary>
        private ImageResizingService.ResizeResult ProcessLockscreenImage(
            WallpaperConfiguration configuration, 
            FilePermissionService permissionService, 
            ImageResizingService imageService, 
            PSCmdlet? cmdlet)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, "Processing lockscreen image");

            // Create lockscreen scratch directory
            var lockscreenScratchDir = new DirectoryInfo(Path.Combine(configuration.ImageScratchDirectory.FullName, "LockScreen"));
            
            // Resize lockscreen image
            var resizeResult = imageService.ResizeLockscreenImage(configuration.LockscreenSourcePath!, lockscreenScratchDir, cmdlet);
            
            if (!resizeResult.Success)
            {
                return resizeResult;
            }

            // Ensure destination directory exists and take ownership
            if (!configuration.LockscreenDestinationPath.Directory!.Exists)
            {
                configuration.LockscreenDestinationPath.Directory.Create();
            }

            permissionService.TakeOwnershipAndGrantAccess(configuration.LockscreenDestinationPath.Directory.FullName, cmdlet);

            // Copy to final destination
            File.Copy(resizeResult.ExportFile.FullName, configuration.LockscreenDestinationPath.FullName, true);
            
            // Update result with final destination
            resizeResult.ExportFile = configuration.LockscreenDestinationPath;
            
            return resizeResult;
        }

        /// <summary>
        /// Processes wallpaper images for multiple resolutions
        /// </summary>
        private List<ImageResizingService.ResizeResult> ProcessWallpaperImages(
            WallpaperConfiguration configuration, 
            FilePermissionService permissionService, 
            ImageResizingService imageService, 
            PSCmdlet? cmdlet)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, "Processing wallpaper images for multiple resolutions");

            // Create wallpaper scratch directory
            var wallpaperScratchDir = new DirectoryInfo(Path.Combine(configuration.ImageScratchDirectory.FullName, "Wallpaper"));
            
            // Resize wallpaper images for all resolutions
            return imageService.ResizeWallpaperImages(configuration.WallpaperSourcePath, wallpaperScratchDir, configuration.ResolutionList, cmdlet);
        }

        /// <summary>
        /// Copies primary wallpaper image
        /// </summary>
        private ImageResizingService.ResizeResult CopyPrimaryWallpaper(
            WallpaperConfiguration configuration, 
            FilePermissionService permissionService, 
            ImageResizingService imageService, 
            PSCmdlet? cmdlet)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, "Configuring primary wallpaper");

            // Create primary wallpaper scratch directory
            var primaryScratchDir = new DirectoryInfo(Path.Combine(configuration.ImageScratchDirectory.FullName, "Primary"));
            
            // Resize to standard resolution (using source dimensions)
            var resizeResult = imageService.ResizeLockscreenImage(configuration.WallpaperSourcePath, primaryScratchDir, cmdlet);
            
            if (!resizeResult.Success)
            {
                return resizeResult;
            }

            // Ensure destination directory exists and take ownership
            if (!configuration.WallpaperDestinationPath.Directory!.Exists)
            {
                configuration.WallpaperDestinationPath.Directory.Create();
            }

            permissionService.TakeOwnershipAndGrantAccess(configuration.WallpaperDestinationPath.Directory.FullName, cmdlet);

            // Copy to final destination
            File.Copy(resizeResult.ExportFile.FullName, configuration.WallpaperDestinationPath.FullName, true);
            
            // Update result with final destination
            resizeResult.ExportFile = configuration.WallpaperDestinationPath;
            
            return resizeResult;
        }

        /// <summary>
        /// Copies default wallpapers to target directory
        /// </summary>
        private void CopyDefaultWallpapers(
            WallpaperConfiguration configuration, 
            FilePermissionService permissionService, 
            List<ImageResizingService.ResizeResult> wallpaperResults, 
            PSCmdlet? cmdlet)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, "Copying default wallpapers to target directory");

            // Ensure destination directory exists and take ownership
            if (!configuration.DefaultWallpapersDestinationPath.Exists)
            {
                configuration.DefaultWallpapersDestinationPath.Create();
            }

            permissionService.TakeOwnershipAndGrantAccess(configuration.DefaultWallpapersDestinationPath.FullName, cmdlet);

            // Copy all successfully resized wallpapers
            foreach (var wallpaperResult in wallpaperResults)
            {
                if (wallpaperResult.Success)
                {
                    try
                    {
                        var destinationPath = Path.Combine(configuration.DefaultWallpapersDestinationPath.FullName, wallpaperResult.ExportFile.Name);
                        File.Copy(wallpaperResult.ExportFile.FullName, destinationPath, true);
                        
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"Copied wallpaper: {wallpaperResult.ExportFile.Name}");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to copy wallpaper {wallpaperResult.ExportFile.Name}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Disposes the service
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
