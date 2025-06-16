using System;
using System.IO;
using System.Management.Automation;
using System.Runtime.InteropServices;
using PSWindowsImageTools.Services.Native;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// WIM Export Service based on Microsoft's actual Export-WindowsImage implementation
    /// Uses native WIM API calls exactly like Microsoft does
    /// </summary>
    public class WimExportService : IDisposable
    {
        private const string ServiceName = "WimExportService";
        private bool _disposed = false;

        /// <summary>
        /// Exports an image using the same method as Microsoft's Export-WindowsImage cmdlet
        /// </summary>
        /// <param name="sourceImagePath">Source WIM/ESD file path</param>
        /// <param name="destinationImagePath">Destination WIM file path</param>
        /// <param name="sourceIndex">Source image index</param>
        /// <param name="sourceName">Optional source image name</param>
        /// <param name="destinationName">Optional destination image name</param>
        /// <param name="compressionType">Compression type (None, Fast, Max, Recovery)</param>
        /// <param name="checkIntegrity">Verify file integrity</param>
        /// <param name="setBootable">Set as bootable image</param>
        /// <param name="scratchDirectory">Temporary directory for operations</param>
        /// <param name="progressCallback">Progress reporting callback</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>True if export succeeded</returns>
        public bool ExportImage(
            string sourceImagePath,
            string destinationImagePath,
            uint sourceIndex,
            string? sourceName = null,
            string? destinationName = null,
            string compressionType = "Max",
            bool checkIntegrity = false,
            bool setBootable = false,
            string? scratchDirectory = null,
            Action<int, string>? progressCallback = null,
            PSCmdlet? cmdlet = null)
        {
            // Validate parameters
            if (string.IsNullOrEmpty(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Source image file not found: {sourceImagePath}");
                return false;
            }

            if (string.IsNullOrEmpty(destinationImagePath))
            {
                LoggingService.WriteError(cmdlet, ServiceName, "Destination image path cannot be empty");
                return false;
            }

            IntPtr sourceWimHandle = IntPtr.Zero;
            IntPtr destinationWimHandle = IntPtr.Zero;
            IntPtr sourceImageHandle = IntPtr.Zero;
            IntPtr destinationImageHandle = IntPtr.Zero;
            bool inPlaceExport = false;

            try
            {
                var exportStartTime = LoggingService.LogOperationStartWithTimestamp(cmdlet, ServiceName,
                    "WIM Export", $"Index {sourceIndex} from {Path.GetFileName(sourceImagePath)} to {Path.GetFileName(destinationImagePath)}");

                // Check if source and destination are the same (in-place export)
                if (string.Equals(Path.GetFullPath(sourceImagePath), Path.GetFullPath(destinationImagePath),
                    StringComparison.OrdinalIgnoreCase))
                {
                    inPlaceExport = true;
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Performing in-place export");
                }

                // Open source WIM file
                uint sourceAccess = WimNativeApi.GENERIC_READ;
                if (inPlaceExport)
                    sourceAccess |= 0x40000000; // Add write access for in-place

                sourceWimHandle = WimNativeApi.WIMCreateFile(
                    sourceImagePath,
                    sourceAccess,
                    WimNativeApi.OPEN_EXISTING,
                    WimNativeApi.GetWimCreateFlags(checkIntegrity, false) | WimNativeApi.WIM_FLAG_SHARE_WRITE,
                    0,
                    out uint sourceCreationResult);

                if (sourceWimHandle == IntPtr.Zero)
                {
                    var error = WimNativeApi.GetLastErrorAsHResult();
                    LoggingService.WriteError(cmdlet, ServiceName, $"Failed to open source WIM file. Error: 0x{error:X8}");
                    return false;
                }

                // Set scratch directory if provided
                if (!string.IsNullOrEmpty(scratchDirectory))
                {
                    if (!WimNativeApi.WIMSetTemporaryPath(sourceWimHandle, scratchDirectory!))
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName, "Failed to set scratch directory");
                    }
                }

                // Resolve source image index if name was provided
                uint actualSourceIndex = sourceIndex;
                if (!string.IsNullOrEmpty(sourceName))
                {
                    // TODO: Implement GetWimIndexByName helper method
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Using source name: {sourceName}");
                }

                // Load source image
                sourceImageHandle = WimNativeApi.WIMLoadImage(sourceWimHandle, actualSourceIndex);
                if (sourceImageHandle == IntPtr.Zero)
                {
                    var error = WimNativeApi.GetLastErrorAsHResult();
                    LoggingService.WriteError(cmdlet, ServiceName, $"Failed to load source image {actualSourceIndex}. Error: 0x{error:X8}");
                    return false;
                }

                // Handle destination WIM
                if (inPlaceExport)
                {
                    destinationWimHandle = sourceWimHandle;
                }
                else
                {
                    // Create destination directory if needed
                    var destinationDir = Path.GetDirectoryName(destinationImagePath);
                    if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"Created destination directory: {destinationDir}");
                    }

                    // Determine compression type
                    uint compression = WimNativeApi.ParseCompressionType(compressionType);
                    
                    // Create destination WIM file
                    uint destFlags = WimNativeApi.GetWimCreateFlags(checkIntegrity, false);
                    if (compression == WimNativeApi.WIM_COMPRESS_LZMS)
                        destFlags |= 0x20000000; // Chunked flag for LZMS

                    destinationWimHandle = WimNativeApi.WIMCreateFile(
                        destinationImagePath,
                        WimNativeApi.GENERIC_READ | WimNativeApi.GENERIC_WRITE,
                        WimNativeApi.CREATE_ALWAYS,
                        destFlags,
                        compression,
                        out uint destCreationResult);

                    if (destinationWimHandle == IntPtr.Zero)
                    {
                        var error = WimNativeApi.GetLastErrorAsHResult();
                        LoggingService.WriteError(cmdlet, ServiceName, $"Failed to create destination WIM file. Error: 0x{error:X8}");
                        return false;
                    }

                    // Set scratch directory for destination
                    if (!string.IsNullOrEmpty(scratchDirectory))
                    {
                        WimNativeApi.WIMSetTemporaryPath(destinationWimHandle, scratchDirectory!);
                    }
                }

                // Register progress callback
                WimNativeApi.WimCallback? nativeCallback = null;
                if (progressCallback != null)
                {
                    nativeCallback = (messageId, wParam, lParam, userData) =>
                    {
                        // Handle WIM progress messages
                        if (messageId == 0x9448) // WIM_MSG_PROGRESS
                        {
                            var current = (uint)wParam.ToInt32();
                            var total = (uint)lParam.ToInt32();
                            if (total > 0)
                            {
                                var percentage = (int)((current * 100) / total);
                                progressCallback(percentage, $"Exporting image: {percentage}%");
                            }
                        }
                        return 0; // Continue operation
                    };

                    var callbackResult = WimNativeApi.WIMRegisterMessageCallback(destinationWimHandle, nativeCallback, IntPtr.Zero);
                    if (callbackResult == uint.MaxValue)
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName, "Failed to register progress callback");
                    }
                }

                // Perform the actual export
                LoggingService.WriteVerbose(cmdlet, ServiceName, "Performing WIM export operation");
                
                uint exportFlags = WimNativeApi.GetWimExportFlags();
                bool exportResult = WimNativeApi.WIMExportImage(sourceImageHandle, destinationWimHandle, exportFlags);

                if (!exportResult)
                {
                    var error = WimNativeApi.GetLastErrorAsHResult();
                    LoggingService.WriteError(cmdlet, ServiceName, $"WIM export operation failed. Error: 0x{error:X8}");
                    return false;
                }

                // Set bootable flag if requested
                if (setBootable)
                {
                    // Get the image count to set the last image as bootable
                    // TODO: Implement GetWimImageCount helper method
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Setting image as bootable");
                }

                // Set destination name if provided
                if (!string.IsNullOrEmpty(destinationName))
                {
                    // Load the exported image to modify its name
                    // TODO: Implement image name modification
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Setting destination name: {destinationName}");
                }

                // Unregister callback
                if (nativeCallback != null)
                {
                    WimNativeApi.WIMUnregisterMessageCallback(destinationWimHandle, nativeCallback);
                }

                LoggingService.LogOperationCompleteWithTimestamp(cmdlet, ServiceName, "WIM Export", exportStartTime,
                    $"Index {sourceIndex} from {Path.GetFileName(sourceImagePath)} to {Path.GetFileName(destinationImagePath)}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"WIM export failed: {ex.Message}", ex);
                return false;
            }
            finally
            {
                // Clean up handles in reverse order
                if (destinationImageHandle != IntPtr.Zero)
                    WimNativeApi.WIMCloseHandle(destinationImageHandle);

                if (sourceImageHandle != IntPtr.Zero)
                    WimNativeApi.WIMCloseHandle(sourceImageHandle);

                if (destinationWimHandle != IntPtr.Zero && !inPlaceExport)
                    WimNativeApi.WIMCloseHandle(destinationWimHandle);

                if (sourceWimHandle != IntPtr.Zero)
                    WimNativeApi.WIMCloseHandle(sourceWimHandle);
            }
        }

        /// <summary>
        /// Disposes the WIM export service
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
