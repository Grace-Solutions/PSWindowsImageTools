using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for resizing images based on resolution specifications
    /// C# implementation following the proven Resize-Image PowerShell function approach
    /// </summary>
    public class ImageResizingService : IDisposable
    {
        private const string ServiceName = "ImageResizingService";
        private bool _disposed = false;

        /// <summary>
        /// Result of an image resize operation
        /// </summary>
        public class ResizeResult
        {
            public FileInfo SourceFile { get; set; } = null!;
            public FileInfo ExportFile { get; set; } = null!;
            public int OriginalWidth { get; set; }
            public int OriginalHeight { get; set; }
            public int NewWidth { get; set; }
            public int NewHeight { get; set; }
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
        }

        /// <summary>
        /// Resizes an image for lockscreen use (single resolution)
        /// </summary>
        /// <param name="sourceImagePath">Source image file</param>
        /// <param name="destinationDirectory">Destination directory</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Resize result</returns>
        public ResizeResult ResizeLockscreenImage(FileInfo sourceImagePath, DirectoryInfo destinationDirectory, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Resizing lockscreen image: {sourceImagePath.FullName}");

                // Lockscreen uses a single resolution - typically the source resolution scaled to 100%
                using var sourceImage = Image.FromFile(sourceImagePath.FullName);
                
                var result = new ResizeResult
                {
                    SourceFile = sourceImagePath,
                    OriginalWidth = sourceImage.Width,
                    OriginalHeight = sourceImage.Height,
                    NewWidth = sourceImage.Width,
                    NewHeight = sourceImage.Height
                };

                // Create destination directory if it doesn't exist
                if (!destinationDirectory.Exists)
                {
                    destinationDirectory.Create();
                }

                var exportPath = new FileInfo(Path.Combine(destinationDirectory.FullName, "LockScreen.jpg"));
                result.ExportFile = exportPath;

                // Create resized image
                using var resizedImage = new Bitmap(result.NewWidth, result.NewHeight);
                using var graphics = Graphics.FromImage(resizedImage);
                
                // Set high quality rendering
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                // Draw the resized image
                graphics.DrawImage(sourceImage, 0, 0, result.NewWidth, result.NewHeight);

                // Save as JPEG
                resizedImage.Save(exportPath.FullName, ImageFormat.Jpeg);

                result.Success = true;
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Successfully resized lockscreen image to {result.NewWidth}x{result.NewHeight}");

                return result;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to resize lockscreen image: {ex.Message}", ex);
                return new ResizeResult
                {
                    SourceFile = sourceImagePath,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Resizes an image for wallpaper use (multiple resolutions)
        /// Following the proven WindowsWallpaper mode approach
        /// </summary>
        /// <param name="sourceImagePath">Source image file</param>
        /// <param name="destinationDirectory">Destination directory</param>
        /// <param name="resolutionList">List of target resolutions</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>List of resize results</returns>
        public List<ResizeResult> ResizeWallpaperImages(FileInfo sourceImagePath, DirectoryInfo destinationDirectory, ResolutionInfo[] resolutionList, PSCmdlet? cmdlet = null)
        {
            var results = new List<ResizeResult>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Resizing wallpaper image: {sourceImagePath.FullName}");
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Target resolutions: {resolutionList.Length}");

                using var sourceImage = Image.FromFile(sourceImagePath.FullName);
                
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Original image resolution: {sourceImage.Width}x{sourceImage.Height}");

                // Create destination directory if it doesn't exist
                if (!destinationDirectory.Exists)
                {
                    destinationDirectory.Create();
                }

                foreach (var resolution in resolutionList)
                {
                    try
                    {
                        var result = new ResizeResult
                        {
                            SourceFile = sourceImagePath,
                            OriginalWidth = sourceImage.Width,
                            OriginalHeight = sourceImage.Height,
                            NewWidth = resolution.Width,
                            NewHeight = resolution.Height
                        };

                        // Generate filename following the proven naming convention
                        var filename = $"{resolution.ImageName}{resolution.Width}x{resolution.Height}.jpg";
                        var exportPath = new FileInfo(Path.Combine(destinationDirectory.FullName, filename));
                        result.ExportFile = exportPath;

                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"Creating {resolution.Width}x{resolution.Height} wallpaper: {filename}");

                        // Create resized image
                        using var resizedImage = new Bitmap(resolution.Width, resolution.Height);
                        using var graphics = Graphics.FromImage(resizedImage);
                        
                        // Set high quality rendering (following your proven approach)
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                        // Draw the resized image
                        graphics.DrawImage(sourceImage, 0, 0, resolution.Width, resolution.Height);

                        // Save as JPEG
                        resizedImage.Save(exportPath.FullName, ImageFormat.Jpeg);

                        result.Success = true;
                        results.Add(result);

                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"Successfully created {filename}");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to create {resolution.Width}x{resolution.Height} wallpaper: {ex.Message}");
                        results.Add(new ResizeResult
                        {
                            SourceFile = sourceImagePath,
                            NewWidth = resolution.Width,
                            NewHeight = resolution.Height,
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Completed wallpaper resizing. {results.FindAll(r => r.Success).Count}/{results.Count} successful");
                return results;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to resize wallpaper images: {ex.Message}", ex);
                results.Add(new ResizeResult
                {
                    SourceFile = sourceImagePath,
                    Success = false,
                    ErrorMessage = ex.Message
                });
                return results;
            }
        }

        /// <summary>
        /// Resizes a single image to specific dimensions
        /// </summary>
        /// <param name="sourceImagePath">Source image file</param>
        /// <param name="destinationPath">Destination file path</param>
        /// <param name="width">Target width</param>
        /// <param name="height">Target height</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Resize result</returns>
        public ResizeResult ResizeImage(FileInfo sourceImagePath, FileInfo destinationPath, int width, int height, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Resizing image {sourceImagePath.FullName} to {width}x{height}");

                using var sourceImage = Image.FromFile(sourceImagePath.FullName);
                
                var result = new ResizeResult
                {
                    SourceFile = sourceImagePath,
                    ExportFile = destinationPath,
                    OriginalWidth = sourceImage.Width,
                    OriginalHeight = sourceImage.Height,
                    NewWidth = width,
                    NewHeight = height
                };

                // Create destination directory if it doesn't exist
                if (destinationPath.Directory != null && !destinationPath.Directory.Exists)
                {
                    destinationPath.Directory.Create();
                }

                // Create resized image
                using var resizedImage = new Bitmap(width, height);
                using var graphics = Graphics.FromImage(resizedImage);
                
                // Set high quality rendering
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                // Draw the resized image
                graphics.DrawImage(sourceImage, 0, 0, width, height);

                // Save as JPEG
                resizedImage.Save(destinationPath.FullName, ImageFormat.Jpeg);

                result.Success = true;
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Successfully resized image to {width}x{height}");

                return result;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to resize image: {ex.Message}", ex);
                return new ResizeResult
                {
                    SourceFile = sourceImagePath,
                    ExportFile = destinationPath,
                    Success = false,
                    ErrorMessage = ex.Message
                };
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
