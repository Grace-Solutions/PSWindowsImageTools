using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Orchestrator service for offline registry operations
    /// Delegates to specialized services based on operation type:
    /// - RegistryPackageService for reading (no hive mounting)
    /// - NativeRegistryService for modifications (with hive mounting and backup)
    /// </summary>
    public class OfflineRegistryService : IDisposable
    {
        private const string ServiceName = "OfflineRegistryService";
        private bool _disposed = false;

        /// <summary>
        /// Reads only essential Windows version information from the registry
        /// This method focuses only on the CurrentVersion key for version details
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing Windows version registry information</returns>
        public Dictionary<string, object> ReadWindowsVersionInfo(string mountPath, PSCmdlet? cmdlet = null)
        {
            try
            {
                using var registryPackageService = new RegistryPackageService();
                return registryPackageService.ReadWindowsVersionOnly(mountPath, cmdlet);
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName,
                    $"Registry reading failed: {ex.Message}");

                return new Dictionary<string, object>
                {
                    ["RegistryReadError"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Reads offline registry information using the Registry package (preferred method)
        /// This method does NOT require mounting registry hives
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing registry information</returns>
        public Dictionary<string, object> ReadOfflineRegistryInfo(string mountPath, PSCmdlet? cmdlet = null)
        {
            try
            {
                // Using Registry package for reading

                using var registryPackageService = new RegistryPackageService();
                return registryPackageService.ReadOfflineRegistry(mountPath, cmdlet);
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName,
                    $"Registry package reading failed, falling back to native API: {ex.Message}");

                // Fallback to native API if Registry package fails
                return ReadOfflineRegistryWithNativeApi(mountPath, cmdlet);
            }
        }

        /// <summary>
        /// Reads offline registry information using native API with hive mounting
        /// Use this method when Registry package fails or for compatibility
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing registry information</returns>
        public Dictionary<string, object> ReadOfflineRegistryWithNativeApi(string mountPath, PSCmdlet? cmdlet = null)
        {
            try
            {
                // Using native API for reading

                using var nativeRegistryService = new NativeRegistryService();
                return nativeRegistryService.ReadOfflineRegistryWithMounting(mountPath, cmdlet);
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, 
                    $"Failed to read registry with native API: {ex.Message}", ex);
                
                return new Dictionary<string, object>
                {
                    ["RegistryReadError"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Modifies offline registry (future implementation)
        /// Uses native API with hive mounting and backup
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="modifications">Registry modifications to apply</param>
        /// <param name="createBackup">Whether to create backup before modification</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>True if modifications were successful</returns>
        public bool ModifyOfflineRegistry(string mountPath, List<RegistryModification> modifications, 
            bool createBackup = true, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Modifying offline registry with {modifications.Count} changes (backup: {createBackup})");

                using var nativeRegistryService = new NativeRegistryService();

                // Create backup if requested
                if (createBackup)
                {
                    var backupPath = System.IO.Path.Combine(mountPath, "..", "registry_backup");
                    if (!nativeRegistryService.BackupRegistryHives(mountPath, backupPath, cmdlet))
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName, 
                            "Failed to create registry backup, proceeding without backup");
                    }
                }

                // Apply modifications
                return nativeRegistryService.ModifyOfflineRegistry(mountPath, modifications, cmdlet);
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, 
                    $"Failed to modify offline registry: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Creates backup of registry hives before modification
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="backupPath">Path where to store backups</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>True if backup was successful</returns>
        public bool BackupRegistryHives(string mountPath, string backupPath, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    "Creating registry hive backup using native service");

                using var nativeRegistryService = new NativeRegistryService();
                return nativeRegistryService.BackupRegistryHives(mountPath, backupPath, cmdlet);
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, 
                    $"Failed to backup registry hives: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Disposes the offline registry service
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
