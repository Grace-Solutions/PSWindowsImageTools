using System;
using System.IO;
using System.Management.Automation;
using PSWindowsImageTools.Services.Native;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Native DISM service for mount/unmount operations with real progress callbacks
    /// Uses only the corrected native DISM API calls that work properly
    /// </summary>
    public class NativeDismService : IDisposable
    {
        private const string ServiceName = "NativeDismService";
        private bool _dismInitialized = false;
        private bool _disposed = false;

        /// <summary>
        /// Initializes the native DISM API
        /// </summary>
        public void Initialize()
        {
            if (!_dismInitialized)
            {
                try
                {
                    // Use the corrected API with PreserveSig = false (throws exceptions instead of returning HRESULTs)
                    DismNativeApi.DismInitialize(DismNativeApi.LogLevel.LogErrors, null, null);
                    _dismInitialized = true;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to initialize DISM API. HRESULT: 0x{ex.HResult:X8}", ex);
                }
            }
        }

        /// <summary>
        /// Mounts an image using native DISM API with real progress callbacks
        /// </summary>
        public bool MountImage(string imageFilePath, string mountPath, uint imageIndex,
            bool readOnly = false, Action<int, string>? progressCallback = null, PSCmdlet? cmdlet = null)
        {
            Initialize();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Mounting image {imageIndex} from {imageFilePath} to {mountPath} (ReadOnly: {readOnly})");

                // Create mount directory if it doesn't exist
                if (!Directory.Exists(mountPath))
                {
                    Directory.CreateDirectory(mountPath);
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Created mount directory: {mountPath}");
                }

                // Create progress callback if provided
                DismNativeApi.ProgressCallback? nativeCallback = null;
                if (progressCallback != null)
                {
                    nativeCallback = (current, total, userData) =>
                    {
                        try
                        {
                            if (total > 0)
                            {
                                var percentage = (int)((current * 100) / total);
                                progressCallback(percentage, $"Mounting image: {percentage}%");
                            }
                            else
                            {
                                progressCallback(-1, "Mounting image...");
                            }
                        }
                        catch
                        {
                            // Never throw exceptions from native callback thread
                        }
                    };
                }

                uint mountFlags = readOnly ? 1u : 0u; // DISM_MOUNT_READONLY = 1, DISM_MOUNT_READWRITE = 0

                DismNativeApi.DismMountImage(
                    imageFilePath,
                    mountPath,
                    imageIndex,
                    null, // imageName
                    DismNativeApi.ImageIdentifier.ImageIndex,
                    mountFlags,
                    IntPtr.Zero, // cancelEvent
                    nativeCallback,
                    IntPtr.Zero); // userData

                LoggingService.WriteVerbose(cmdlet, ServiceName, "Image mounted successfully using native API");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to mount image: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Unmounts an image using native DISM API with progress reporting
        /// </summary>
        public bool UnmountImage(string mountPath, bool commitChanges = false, Action<int, string>? progressCallback = null, PSCmdlet? cmdlet = null)
        {
            Initialize();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Unmounting image from {mountPath} (CommitChanges: {commitChanges})");

                // Create progress callback if provided
                DismNativeApi.ProgressCallback? nativeCallback = null;
                if (progressCallback != null)
                {
                    nativeCallback = (current, total, userData) =>
                    {
                        try
                        {
                            if (total > 0)
                            {
                                var percentage = (int)((current * 100) / total);
                                progressCallback(percentage, $"Unmounting image: {percentage}%");
                            }
                            else
                            {
                                progressCallback(-1, "Unmounting image...");
                            }
                        }
                        catch
                        {
                            // Never throw exceptions from native callback thread
                        }
                    };
                }

                uint unmountFlags = commitChanges ? DismNativeApi.UnmountFlags.DISM_COMMIT_IMAGE : DismNativeApi.UnmountFlags.DISM_DISCARD_IMAGE;

                try
                {
                    DismNativeApi.DismUnmountImage(
                        mountPath,
                        unmountFlags,
                        IntPtr.Zero, // cancelEvent
                        nativeCallback,
                        IntPtr.Zero); // userData

                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Image unmounted successfully using native API");
                    return true;
                }
                catch (Exception ex) when (ex.HResult == unchecked((int)0xC142010C))
                {
                    // Image is still in use - try to force discard
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Image in use, attempting force discard...");

                    try
                    {
                        DismNativeApi.DismUnmountImage(
                            mountPath,
                            DismNativeApi.UnmountFlags.DISM_DISCARD_IMAGE,
                            IntPtr.Zero,
                            nativeCallback,
                            IntPtr.Zero);

                        LoggingService.WriteVerbose(cmdlet, ServiceName, "Image force unmounted (discarded) successfully");
                        return true;
                    }
                    catch
                    {
                        // If force discard also fails, rethrow original exception
                        throw ex;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to unmount image: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Disposes the native DISM service
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_dismInitialized)
                {
                    try { DismNativeApi.DismShutdown(); } catch { }
                    _dismInitialized = false;
                }
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}