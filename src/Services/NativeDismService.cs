using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using PSWindowsImageTools.Services.Native;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Complete native DISM service using direct P/Invoke calls to dismapi.dll
    /// Provides ALL DISM functionality without limitations of Microsoft.Dism wrapper
    /// </summary>
    public class NativeDismService : IDisposable
    {
        private const string ServiceName = "NativeDismService";
        private bool _dismInitialized = false;
        private bool _disposed = false;
        private readonly Dictionary<string, uint> _activeSessions = new Dictionary<string, uint>();

        /// <summary>
        /// Initializes the native DISM API
        /// </summary>
        /// <param name="logLevel">DISM log level</param>
        /// <param name="logFilePath">Optional log file path</param>
        /// <param name="scratchDirectory">Optional scratch directory</param>
        public void Initialize(DismNativeApi.LogLevel logLevel = DismNativeApi.LogLevel.LogErrors,
            string? logFilePath = null, string? scratchDirectory = null)
        {
            if (!_dismInitialized)
            {
                var result = DismNativeApi.DismInitialize(logLevel, logFilePath ?? string.Empty, scratchDirectory ?? string.Empty);
                if (result != 0)
                {
                    throw new InvalidOperationException($"Failed to initialize DISM API. HRESULT: 0x{result:X8}");
                }
                _dismInitialized = true;
            }
        }

        #region Image Information

        /// <summary>
        /// Gets comprehensive image information using native DISM API
        /// </summary>
        /// <param name="imagePath">Path to WIM/ESD file</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>List of detailed image information</returns>
        public List<NativeImageInfo> GetImageInfo(string imagePath, PSCmdlet? cmdlet = null)
        {
            Initialize();
            var imageInfoList = new List<NativeImageInfo>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Getting native image information from: {imagePath}");

                var result = DismNativeApi.DismGetImageInfo(imagePath, out var imageInfoPtr, out var count);
                if (result != 0)
                {
                    throw new InvalidOperationException($"Failed to get image info. HRESULT: 0x{result:X8}");
                }

                try
                {
                    // Parse the native image info structures
                    var imageInfoSize = Marshal.SizeOf<DismNativeApi.DismImageInfo>();
                    for (int i = 0; i < count; i++)
                    {
                        var currentPtr = IntPtr.Add(imageInfoPtr, i * imageInfoSize);
                        var nativeInfo = Marshal.PtrToStructure<DismNativeApi.DismImageInfo>(currentPtr);

                        var imageInfo = new NativeImageInfo
                        {
                            ImageIndex = nativeInfo.ImageIndex,
                            ImageName = DismNativeApi.PtrToStringUni(nativeInfo.ImageName),
                            ImageDescription = DismNativeApi.PtrToStringUni(nativeInfo.ImageDescription),
                            ImageSize = nativeInfo.ImageSize,
                            Architecture = nativeInfo.Architecture,
                            ProductName = DismNativeApi.PtrToStringUni(nativeInfo.ProductName),
                            EditionId = DismNativeApi.PtrToStringUni(nativeInfo.EditionId),
                            InstallationType = DismNativeApi.PtrToStringUni(nativeInfo.InstallationType),
                            Hal = DismNativeApi.PtrToStringUni(nativeInfo.Hal),
                            ProductType = DismNativeApi.PtrToStringUni(nativeInfo.ProductType),
                            ProductSuite = DismNativeApi.PtrToStringUni(nativeInfo.ProductSuite),
                            MajorVersion = nativeInfo.MajorVersion,
                            MinorVersion = nativeInfo.MinorVersion,
                            Build = nativeInfo.Build,
                            SpBuild = nativeInfo.SpBuild,
                            SpLevel = DismNativeApi.PtrToStringUni(nativeInfo.SpLevel),
                            Bootable = nativeInfo.Bootable,
                            SystemRoot = DismNativeApi.PtrToStringUni(nativeInfo.SystemRoot),
                            DefaultLanguage = DismNativeApi.PtrToStringUni(nativeInfo.DefaultLanguage),
                            CreatedTime = DismNativeApi.PtrToStringUni(nativeInfo.CreatedTime),
                            ModifiedTime = DismNativeApi.PtrToStringUni(nativeInfo.ModifiedTime),
                            ImageType = nativeInfo.ImageType,
                            SourcePath = imagePath
                        };

                        // Parse languages array if present
                        if (nativeInfo.LanguageCount > 0 && nativeInfo.Languages != IntPtr.Zero)
                        {
                            imageInfo.Languages = new List<string>();
                            var languagePtrSize = IntPtr.Size;
                            for (int j = 0; j < nativeInfo.LanguageCount; j++)
                            {
                                var langPtr = Marshal.ReadIntPtr(nativeInfo.Languages, j * languagePtrSize);
                                var language = DismNativeApi.PtrToStringUni(langPtr);
                                if (!string.IsNullOrEmpty(language))
                                {
                                    imageInfo.Languages.Add(language);
                                }
                            }
                        }

                        imageInfoList.Add(imageInfo);
                    }

                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found {imageInfoList.Count} images using native API");
                }
                finally
                {
                    // Clean up native memory
                    if (imageInfoPtr != IntPtr.Zero)
                    {
                        DismNativeApi.DismDelete(imageInfoPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to get native image info: {ex.Message}", ex);
                throw;
            }

            return imageInfoList;
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Opens a session to a mounted image using native DISM API
        /// </summary>
        /// <param name="imagePath">Path to mounted image or WIM file</param>
        /// <param name="windowsDirectory">Windows directory (for mounted images)</param>
        /// <param name="systemRoot">System root (for mounted images)</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>Session identifier</returns>
        public string OpenSession(string imagePath, string? windowsDirectory = null, string? systemRoot = null, PSCmdlet? cmdlet = null)
        {
            Initialize();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Opening native DISM session for: {imagePath}");

                var result = DismNativeApi.DismOpenSession(
                    imagePath,
                    windowsDirectory ?? string.Empty,
                    systemRoot ?? string.Empty,
                    out var session);

                if (result != 0)
                {
                    throw new InvalidOperationException($"Failed to open DISM session. HRESULT: 0x{result:X8}");
                }

                var sessionId = Guid.NewGuid().ToString();
                _activeSessions[sessionId] = session;

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Opened session: {sessionId}");
                return sessionId;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to open session: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Closes a DISM session
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        public void CloseSession(string sessionId, PSCmdlet? cmdlet = null)
        {
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                try
                {
                    var result = DismNativeApi.DismCloseSession(session);
                    if (result != 0)
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to close session {sessionId}. HRESULT: 0x{result:X8}");
                    }
                    else
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"Closed session: {sessionId}");
                    }
                }
                finally
                {
                    _activeSessions.Remove(sessionId);
                }
            }
        }

        #endregion

        #region Mount Operations

        /// <summary>
        /// Mounts an image using native DISM API with full progress reporting
        /// </summary>
        /// <param name="imageFilePath">Path to WIM/ESD file</param>
        /// <param name="mountPath">Mount directory</param>
        /// <param name="imageIndex">Image index to mount</param>
        /// <param name="imageName">Optional image name</param>
        /// <param name="readOnly">Mount as read-only</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>True if mount succeeded</returns>
        public bool MountImage(string imageFilePath, string mountPath, uint imageIndex, string? imageName = null, 
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

                // Create progress callback wrapper
                DismNativeApi.ProgressCallback? nativeCallback = null;
                if (progressCallback != null)
                {
                    nativeCallback = (current, total, userData) =>
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
                    };
                }

                var result = DismNativeApi.DismMountImage(
                    imageFilePath,
                    mountPath,
                    imageIndex,
                    imageName ?? string.Empty,
                    DismNativeApi.ImageIdentifier.ImageIndex,
                    0, // Mount flags
                    null, // Cancel handle
                    nativeCallback!,
                    IntPtr.Zero);

                if (result != 0)
                {
                    LoggingService.WriteError(cmdlet, ServiceName, $"Failed to mount image. HRESULT: 0x{result:X8}");
                    return false;
                }

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
        /// <param name="mountPath">Mount directory to unmount</param>
        /// <param name="commitChanges">Whether to commit changes</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>True if unmount succeeded</returns>
        public bool UnmountImage(string mountPath, bool commitChanges = false, Action<int, string>? progressCallback = null, PSCmdlet? cmdlet = null)
        {
            Initialize();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Unmounting image from {mountPath} (CommitChanges: {commitChanges})");

                // Create progress callback wrapper
                DismNativeApi.ProgressCallback? nativeCallback = null;
                if (progressCallback != null)
                {
                    nativeCallback = (current, total, userData) =>
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
                    };
                }

                uint unmountFlags = commitChanges ? 1u : 0u; // DISM_COMMIT_IMAGE = 1, DISM_DISCARD_IMAGE = 0

                var result = DismNativeApi.DismUnmountImage(
                    mountPath,
                    unmountFlags,
                    null, // Cancel handle
                    nativeCallback!,
                    IntPtr.Zero);

                if (result != 0)
                {
                    LoggingService.WriteError(cmdlet, ServiceName, $"Failed to unmount image. HRESULT: 0x{result:X8}");
                    return false;
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, "Image unmounted successfully using native API");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to unmount image: {ex.Message}", ex);
                return false;
            }
        }

        #endregion

        #region Package Management

        /// <summary>
        /// Adds a package to a mounted image using native DISM API
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        /// <param name="packagePath">Path to package file (.cab, .msu)</param>
        /// <param name="ignoreCheck">Ignore applicability checks</param>
        /// <param name="preventPending">Prevent pending operations</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>True if package was added successfully</returns>
        public bool AddPackage(string sessionId, string packagePath, bool ignoreCheck = false, bool preventPending = false,
            Action<int, string>? progressCallback = null, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Adding package {packagePath} to session {sessionId}");

                var session = GetSession(sessionId);

                // Create progress callback wrapper
                DismNativeApi.ProgressCallback? nativeCallback = null;
                if (progressCallback != null)
                {
                    nativeCallback = (current, total, userData) =>
                    {
                        if (total > 0)
                        {
                            var percentage = (int)((current * 100) / total);
                            progressCallback(percentage, $"Installing package: {percentage}%");
                        }
                        else
                        {
                            progressCallback(-1, "Installing package...");
                        }
                    };
                }

                var result = DismNativeApi.DismAddPackage(
                    session,
                    packagePath,
                    ignoreCheck,
                    preventPending,
                    null, // Cancel handle
                    nativeCallback!,
                    IntPtr.Zero);

                if (result != 0)
                {
                    LoggingService.WriteError(cmdlet, ServiceName, $"Failed to add package. HRESULT: 0x{result:X8}");
                    return false;
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, "Package added successfully using native API");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to add package: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Removes a package from a mounted image using native DISM API
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        /// <param name="packageIdentifier">Package name or path</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>True if package was removed successfully</returns>
        public bool RemovePackage(string sessionId, string packageIdentifier,
            Action<int, string>? progressCallback = null, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Removing package {packageIdentifier} from session {sessionId}");

                var session = GetSession(sessionId);

                // Create progress callback wrapper
                DismNativeApi.ProgressCallback? nativeCallback = null;
                if (progressCallback != null)
                {
                    nativeCallback = (current, total, userData) =>
                    {
                        if (total > 0)
                        {
                            var percentage = (int)((current * 100) / total);
                            progressCallback(percentage, $"Removing package: {percentage}%");
                        }
                        else
                        {
                            progressCallback(-1, "Removing package...");
                        }
                    };
                }

                var result = DismNativeApi.DismRemovePackage(
                    session,
                    packageIdentifier,
                    DismNativeApi.PackageIdentifier.PackageName,
                    null, // Cancel handle
                    nativeCallback!,
                    IntPtr.Zero);

                if (result != 0)
                {
                    LoggingService.WriteError(cmdlet, ServiceName, $"Failed to remove package. HRESULT: 0x{result:X8}");
                    return false;
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, "Package removed successfully using native API");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to remove package: {ex.Message}", ex);
                return false;
            }
        }

        #endregion

        #region Feature Management

        /// <summary>
        /// Enables a Windows feature using native DISM API
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        /// <param name="featureName">Feature name to enable</param>
        /// <param name="enableAll">Enable all parent features</param>
        /// <param name="sourcePaths">Source paths for feature files</param>
        /// <param name="limitAccess">Limit access to Windows Update</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>True if feature was enabled successfully</returns>
        public bool EnableFeature(string sessionId, string featureName, bool enableAll = false,
            string[]? sourcePaths = null, bool limitAccess = false,
            Action<int, string>? progressCallback = null, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Enabling feature {featureName} in session {sessionId}");

                var session = GetSession(sessionId);

                // Create progress callback wrapper
                DismNativeApi.ProgressCallback? nativeCallback = null;
                if (progressCallback != null)
                {
                    nativeCallback = (current, total, userData) =>
                    {
                        if (total > 0)
                        {
                            var percentage = (int)((current * 100) / total);
                            progressCallback(percentage, $"Enabling feature: {percentage}%");
                        }
                        else
                        {
                            progressCallback(-1, "Enabling feature...");
                        }
                    };
                }

                // Handle source paths
                IntPtr sourcePathsPtr = IntPtr.Zero;
                uint sourcePathCount = 0;

                if (sourcePaths != null && sourcePaths.Length > 0)
                {
                    sourcePathCount = (uint)sourcePaths.Length;
                    sourcePathsPtr = Marshal.AllocHGlobal(IntPtr.Size * sourcePaths.Length);

                    for (int i = 0; i < sourcePaths.Length; i++)
                    {
                        var pathPtr = Marshal.StringToHGlobalUni(sourcePaths[i]);
                        Marshal.WriteIntPtr(sourcePathsPtr, i * IntPtr.Size, pathPtr);
                    }
                }

                try
                {
                    var result = DismNativeApi.DismEnableFeature(
                        session,
                        featureName,
                        string.Empty, // identifier
                        DismNativeApi.PackageIdentifier.PackageName,
                        limitAccess,
                        sourcePaths ?? new string[0],
                        sourcePathCount,
                        enableAll,
                        null, // Cancel handle
                        nativeCallback!,
                        IntPtr.Zero);

                    if (result != 0)
                    {
                        LoggingService.WriteError(cmdlet, ServiceName, $"Failed to enable feature. HRESULT: 0x{result:X8}");
                        return false;
                    }

                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Feature enabled successfully using native API");
                    return true;
                }
                finally
                {
                    // Clean up source paths memory
                    if (sourcePathsPtr != IntPtr.Zero)
                    {
                        for (int i = 0; i < sourcePathCount; i++)
                        {
                            var pathPtr = Marshal.ReadIntPtr(sourcePathsPtr, i * IntPtr.Size);
                            if (pathPtr != IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(pathPtr);
                            }
                        }
                        Marshal.FreeHGlobal(sourcePathsPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to enable feature: {ex.Message}", ex);
                return false;
            }
        }

        #endregion

        /// <summary>
        /// Gets the native session for a session ID
        /// </summary>
        private uint GetSession(string sessionId)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                throw new ArgumentException($"Session not found: {sessionId}");
            }
            return session;
        }

        /// <summary>
        /// Disposes the native DISM service
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Close all active sessions
                foreach (var sessionId in _activeSessions.Keys.ToArray())
                {
                    CloseSession(sessionId);
                }

                if (_dismInitialized)
                {
                    try
                    {
                        DismNativeApi.DismShutdown();
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

    /// <summary>
    /// Native image information structure with complete details
    /// </summary>
    public class NativeImageInfo
    {
        public uint ImageIndex { get; set; }
        public string ImageName { get; set; } = string.Empty;
        public string ImageDescription { get; set; } = string.Empty;
        public ulong ImageSize { get; set; }
        public uint Architecture { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string EditionId { get; set; } = string.Empty;
        public string InstallationType { get; set; } = string.Empty;
        public string Hal { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public string ProductSuite { get; set; } = string.Empty;
        public uint MajorVersion { get; set; }
        public uint MinorVersion { get; set; }
        public uint Build { get; set; }
        public uint SpBuild { get; set; }
        public string SpLevel { get; set; } = string.Empty;
        public uint Bootable { get; set; }
        public string SystemRoot { get; set; } = string.Empty;
        public List<string> Languages { get; set; } = new List<string>();
        public string DefaultLanguage { get; set; } = string.Empty;
        public string CreatedTime { get; set; } = string.Empty;
        public string ModifiedTime { get; set; } = string.Empty;
        public uint ImageType { get; set; }
        public string SourcePath { get; set; } = string.Empty;
    }
}
