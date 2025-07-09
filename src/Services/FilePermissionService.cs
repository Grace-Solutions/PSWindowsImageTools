using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Management.Automation;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for managing file and directory permissions using native Windows APIs
    /// Handles TrustedInstaller ownership and permission restoration
    /// </summary>
    public class FilePermissionService : IDisposable
    {
        private const string ServiceName = "FilePermissionService";
        private bool _disposed = false;

        #region Native API Declarations

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetNamedSecurityInfo(
            string pObjectName,
            SE_OBJECT_TYPE ObjectType,
            SECURITY_INFORMATION SecurityInfo,
            IntPtr psidOwner,
            IntPtr psidGroup,
            IntPtr pDacl,
            IntPtr pSacl);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern uint GetNamedSecurityInfo(
            string pObjectName,
            SE_OBJECT_TYPE ObjectType,
            SECURITY_INFORMATION SecurityInfo,
            out IntPtr ppsidOwner,
            out IntPtr ppsidGroup,
            out IntPtr ppDacl,
            out IntPtr ppSecurityDescriptor);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        private enum SE_OBJECT_TYPE
        {
            SE_FILE_OBJECT = 1
        }

        [Flags]
        private enum SECURITY_INFORMATION : uint
        {
            OWNER_SECURITY_INFORMATION = 0x00000001,
            GROUP_SECURITY_INFORMATION = 0x00000002,
            DACL_SECURITY_INFORMATION = 0x00000004,
            SACL_SECURITY_INFORMATION = 0x00000008
        }

        #endregion

        /// <summary>
        /// Stores original security information for restoration
        /// </summary>
        private class SecurityBackup
        {
            public string Path { get; set; } = string.Empty;
            public DirectorySecurity? OriginalSecurity { get; set; }
            public FileSecurity? OriginalFileSecurity { get; set; }
            public bool IsDirectory { get; set; }
        }

        private readonly System.Collections.Generic.List<SecurityBackup> _securityBackups = new System.Collections.Generic.List<SecurityBackup>();

        /// <summary>
        /// Takes ownership of a directory and grants full control to the current user
        /// Backs up original permissions for later restoration
        /// </summary>
        /// <param name="directoryPath">Path to the directory</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>True if successful</returns>
        public bool TakeOwnershipAndGrantAccess(string directoryPath, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Taking ownership of directory: {directoryPath}");

                var dirInfo = new DirectoryInfo(directoryPath);
                if (!dirInfo.Exists)
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, $"Directory does not exist: {directoryPath}");
                    return false;
                }

                // Backup original security
                var backup = new SecurityBackup
                {
                    Path = directoryPath,
                    IsDirectory = true
                };

                try
                {
                    backup.OriginalSecurity = dirInfo.GetAccessControl();
                    _securityBackups.Add(backup);
                }
                catch (Exception ex)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Could not backup original security for {directoryPath}: {ex.Message}");
                }

                // Get current user SID
                var currentUser = WindowsIdentity.GetCurrent();
                var currentUserSid = currentUser.User;

                if (currentUserSid == null)
                {
                    LoggingService.WriteError(cmdlet, ServiceName, "Could not get current user SID", null);
                    return false;
                }

                // Take ownership using native API
                var sidBytes = new byte[currentUserSid.BinaryLength];
                currentUserSid.GetBinaryForm(sidBytes, 0);

                var sidPtr = Marshal.AllocHGlobal(sidBytes.Length);
                try
                {
                    Marshal.Copy(sidBytes, 0, sidPtr, sidBytes.Length);

                    bool result = SetNamedSecurityInfo(
                        directoryPath,
                        SE_OBJECT_TYPE.SE_FILE_OBJECT,
                        SECURITY_INFORMATION.OWNER_SECURITY_INFORMATION,
                        sidPtr,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero);

                    if (!result)
                    {
                        var error = Marshal.GetLastWin32Error();
                        LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to take ownership of {directoryPath}. Win32 Error: {error}");
                        return false;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(sidPtr);
                }

                // Grant full control to current user
                try
                {
                    var security = dirInfo.GetAccessControl();
                    var accessRule = new FileSystemAccessRule(
                        currentUser.Name,
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow);

                    security.SetAccessRule(accessRule);
                    dirInfo.SetAccessControl(security);

                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Successfully granted full control to {currentUser.Name} for {directoryPath}");
                }
                catch (Exception ex)
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to grant access to {directoryPath}: {ex.Message}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Error taking ownership of {directoryPath}: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Takes ownership of a file and grants full control to the current user
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>True if successful</returns>
        public bool TakeFileOwnershipAndGrantAccess(string filePath, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Taking ownership of file: {filePath}");

                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, $"File does not exist: {filePath}");
                    return false;
                }

                // Backup original security
                var backup = new SecurityBackup
                {
                    Path = filePath,
                    IsDirectory = false
                };

                try
                {
                    backup.OriginalFileSecurity = fileInfo.GetAccessControl();
                    _securityBackups.Add(backup);
                }
                catch (Exception ex)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Could not backup original security for {filePath}: {ex.Message}");
                }

                // Get current user
                var currentUser = WindowsIdentity.GetCurrent();

                // Grant full control to current user
                try
                {
                    var security = fileInfo.GetAccessControl();
                    var accessRule = new FileSystemAccessRule(
                        currentUser.Name,
                        FileSystemRights.FullControl,
                        AccessControlType.Allow);

                    security.SetAccessRule(accessRule);
                    fileInfo.SetAccessControl(security);

                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Successfully granted full control to {currentUser.Name} for {filePath}");
                }
                catch (Exception ex)
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to grant access to {filePath}: {ex.Message}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Error taking ownership of {filePath}: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Restores original permissions for all modified paths
        /// </summary>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        public void RestoreOriginalPermissions(PSCmdlet? cmdlet = null)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, $"Restoring original permissions for {_securityBackups.Count} paths");

            foreach (var backup in _securityBackups)
            {
                try
                {
                    if (backup.IsDirectory && backup.OriginalSecurity != null)
                    {
                        var dirInfo = new DirectoryInfo(backup.Path);
                        if (dirInfo.Exists)
                        {
                            dirInfo.SetAccessControl(backup.OriginalSecurity);
                            LoggingService.WriteVerbose(cmdlet, ServiceName, $"Restored permissions for directory: {backup.Path}");
                        }
                    }
                    else if (!backup.IsDirectory && backup.OriginalFileSecurity != null)
                    {
                        var fileInfo = new FileInfo(backup.Path);
                        if (fileInfo.Exists)
                        {
                            fileInfo.SetAccessControl(backup.OriginalFileSecurity);
                            LoggingService.WriteVerbose(cmdlet, ServiceName, $"Restored permissions for file: {backup.Path}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to restore permissions for {backup.Path}: {ex.Message}");
                }
            }

            _securityBackups.Clear();
        }

        /// <summary>
        /// Disposes the service and restores permissions
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                RestoreOriginalPermissions();
                _disposed = true;
            }
        }
    }
}
