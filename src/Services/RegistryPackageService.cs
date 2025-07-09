using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Registry;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for reading offline registry using Registry package with RegistryHiveOnDemand
    /// This service does NOT require mounting registry hives
    /// </summary>
    public class RegistryPackageService : IDisposable
    {
        private const string ServiceName = "RegistryPackageService";
        private bool _disposed = false;

        /// <summary>
        /// Reads Windows version information from registry hive file
        /// </summary>
        /// <param name="hiveFilePath">Path to the SOFTWARE hive file</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing Windows version information</returns>
        public Dictionary<string, object> ReadWindowsVersionInfoFromHive(string hiveFilePath, PSCmdlet? cmdlet = null)
        {
            var versionInfo = new Dictionary<string, object>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Reading Windows version information directly from hive: {hiveFilePath}");

                if (!File.Exists(hiveFilePath))
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName,
                        $"SOFTWARE hive not found at: {hiveFilePath}");
                    return versionInfo;
                }

                var hive = new RegistryHiveOnDemand(hiveFilePath);
                var currentVersionKey = hive.GetKey(@"Microsoft\Windows NT\CurrentVersion");

                if (currentVersionKey != null)
                {
                    var values = currentVersionKey.Values;

                    foreach (var value in values)
                    {
                        versionInfo[value.ValueName] = value.ValueData;
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Successfully read {versionInfo.Count} version properties");

                // Force cleanup
                hive = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    $"Failed to read Windows version information from hive: {ex.Message}", ex);
            }

            return versionInfo;
        }

        /// <summary>
        /// Reads Windows version information from offline registry
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing Windows version information</returns>
        public Dictionary<string, object> ReadWindowsVersionInfo(string mountPath, PSCmdlet? cmdlet = null)
        {
            var versionInfo = new Dictionary<string, object>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Reading Windows version info using RegistryHiveOnDemand from: {mountPath}");

                var softwareHivePath = Path.Combine(mountPath, "Windows", "System32", "config", "SOFTWARE");

                if (!File.Exists(softwareHivePath))
                {
                    // Detailed debugging for why File.Exists() returns false
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"File.Exists('{softwareHivePath}') returned false");

                    try
                    {
                        var fileInfo = new FileInfo(softwareHivePath);
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"FileInfo.Exists: {fileInfo.Exists}");
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"FileInfo.Length: {(fileInfo.Exists ? fileInfo.Length.ToString() : "N/A")}");

                        var dirInfo = new DirectoryInfo(Path.GetDirectoryName(softwareHivePath)!);
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"Parent directory exists: {dirInfo.Exists}");

                        if (dirInfo.Exists)
                        {
                            var files = dirInfo.GetFiles().Select(f => f.Name);
                            LoggingService.WriteVerbose(cmdlet, ServiceName, $"Files in config directory: {string.Join(", ", files)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"Error checking file: {ex.Message}");
                    }

                    LoggingService.WriteWarning(cmdlet, ServiceName,
                        $"SOFTWARE hive not found at: {softwareHivePath}");
                    return versionInfo;
                }

                var hive = new RegistryHiveOnDemand(softwareHivePath);
                var currentVersionKey = hive.GetKey(@"Microsoft\Windows NT\CurrentVersion");
                
                if (currentVersionKey != null)
                {
                    var values = currentVersionKey.Values;
                    
                    foreach (var value in values)
                    {
                        versionInfo[value.ValueName] = value.ValueData;
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Successfully read {versionInfo.Count} version properties");

                // Force cleanup of registry hive to release file handles
                hive = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName,
                    $"Failed to read Windows version info: {ex.Message}");
            }

            return versionInfo;
        }

        /// <summary>
        /// Reads offline registry information using RegistryHiveOnDemand
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing registry information</returns>
        public Dictionary<string, object> ReadOfflineRegistry(string mountPath, PSCmdlet? cmdlet = null)
        {
            var registryInfo = new Dictionary<string, object>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Reading offline registry using RegistryHiveOnDemand from: {mountPath}");

                // Read Windows version information
                var versionInfo = ReadWindowsVersionInfo(mountPath, cmdlet);
                foreach (var item in versionInfo)
                {
                    registryInfo[item.Key] = item.Value;
                }

                // Note: Software reading is now handled by GetInstalledSoftware() method

                // Read Windows Update configuration
                var wuConfigInfo = ReadWindowsUpdateConfiguration(mountPath, cmdlet);
                foreach (var item in wuConfigInfo)
                {
                    registryInfo[item.Key] = item.Value;
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Successfully read offline registry with {registryInfo.Count} total properties");
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, 
                    $"Failed to read offline registry: {ex.Message}");
            }

            return registryInfo;
        }





        /// <summary>
        /// Gets installed software as a list of Software objects from a hive file
        /// </summary>
        /// <param name="hiveFilePath">Path to the SOFTWARE hive file</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>List of installed software</returns>
        public List<Software> GetInstalledSoftwareFromHive(string hiveFilePath, PSCmdlet? cmdlet = null)
        {
            var softwareList = new List<Software>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Reading installed software directly from hive: {hiveFilePath}");

                if (!File.Exists(hiveFilePath))
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName,
                        $"SOFTWARE hive not found at: {hiveFilePath}");
                    return softwareList;
                }

                var hive = new RegistryHiveOnDemand(hiveFilePath);

                // Read from regular uninstall key
                ReadUninstallKeyForSoftware(hive, @"Microsoft\Windows\CurrentVersion\Uninstall", softwareList, cmdlet);

                // Read from WOW64 uninstall key for 32-bit apps on 64-bit systems
                ReadUninstallKeyForSoftware(hive, @"WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", softwareList, cmdlet);

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Found {softwareList.Count} installed software entries");

                // Force cleanup
                hive = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    $"Failed to read installed software from hive: {ex.Message}", ex);
            }

            return softwareList;
        }

        /// <summary>
        /// Gets installed software as a list of Software objects
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>List of installed software</returns>
        public List<Software> GetInstalledSoftware(string mountPath, PSCmdlet? cmdlet = null)
        {
            var softwareList = new List<Software>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Reading installed software using RegistryHiveOnDemand from: {mountPath}");

                var softwareHivePath = Path.Combine(mountPath, "Windows", "System32", "config", "SOFTWARE");

                if (!File.Exists(softwareHivePath))
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"GetInstalledSoftware - Mount path exists: {Directory.Exists(mountPath)}");
                    LoggingService.WriteWarning(cmdlet, ServiceName,
                        $"SOFTWARE hive not found at: {softwareHivePath}");
                    return softwareList;
                }

                var hive = new RegistryHiveOnDemand(softwareHivePath);

                // Read from regular uninstall key
                ReadUninstallKeyForSoftware(hive, @"Microsoft\Windows\CurrentVersion\Uninstall", softwareList, cmdlet);

                // Read from WOW64 uninstall key
                ReadUninstallKeyForSoftware(hive, @"WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", softwareList, cmdlet);

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Found {softwareList.Count} installed software entries");

                // Force cleanup of registry hive to release file handles
                hive = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName,
                    $"Failed to read installed software: {ex.Message}");
            }

            return softwareList;
        }

        /// <summary>
        /// Reads Windows Update configuration from registry hive file
        /// </summary>
        /// <param name="hiveFilePath">Path to the SOFTWARE hive file</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing Windows Update configuration</returns>
        public Dictionary<string, object> ReadWindowsUpdateConfigurationFromHive(string hiveFilePath, PSCmdlet? cmdlet)
        {
            var wuConfigInfo = new Dictionary<string, object>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Reading Windows Update configuration directly from hive: {hiveFilePath}");

                if (!File.Exists(hiveFilePath))
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName,
                        $"SOFTWARE hive not found at: {hiveFilePath}");
                    return wuConfigInfo;
                }

                var hive = new RegistryHiveOnDemand(hiveFilePath);
                var wuKey = hive.GetKey(@"Policies\Microsoft\Windows\WindowsUpdate");

                if (wuKey != null)
                {
                    foreach (var value in wuKey.Values)
                    {
                        wuConfigInfo[value.ValueName] = value.ValueData;
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Read {wuConfigInfo.Count} Windows Update configuration properties");

                // Force cleanup
                hive = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    $"Failed to read Windows Update configuration from hive: {ex.Message}", ex);
            }

            return wuConfigInfo;
        }

        /// <summary>
        /// Reads Windows Update configuration from registry
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing Windows Update configuration</returns>
        public Dictionary<string, object> ReadWindowsUpdateConfiguration(string mountPath, PSCmdlet? cmdlet)
        {
            var wuConfigInfo = new Dictionary<string, object>();

            try
            {
                var softwareHivePath = Path.Combine(mountPath, "Windows", "System32", "config", "SOFTWARE");

                if (!File.Exists(softwareHivePath))
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"ReadWindowsUpdateConfiguration - SOFTWARE hive not found at: {softwareHivePath}");
                    return wuConfigInfo;
                }

                var hive = new RegistryHiveOnDemand(softwareHivePath);
                var wuKey = hive.GetKey(@"Policies\Microsoft\Windows\WindowsUpdate");
                
                if (wuKey != null)
                {
                    foreach (var value in wuKey.Values)
                    {
                        wuConfigInfo[value.ValueName] = value.ValueData;
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Read {wuConfigInfo.Count} Windows Update configuration properties");

                // Force cleanup of registry hive to release file handles
                hive = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Error reading Windows Update config: {ex.Message}");
            }

            return wuConfigInfo;
        }

        /// <summary>
        /// Gets a string value from a registry key
        /// </summary>
        /// <param name="key">Registry key</param>
        /// <param name="valueName">Value name</param>
        /// <returns>String value or null if not found</returns>
        private string? GetStringValue(object key, string valueName)
        {
            try
            {
                // Use reflection to access Values property
                var valuesProperty = key.GetType().GetProperty("Values");
                if (valuesProperty != null)
                {
                    var values = valuesProperty.GetValue(key);
                    if (values is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var value in enumerable)
                        {
                            var valueNameProperty = value.GetType().GetProperty("ValueName");
                            var valueDataProperty = value.GetType().GetProperty("ValueData");

                            if (valueNameProperty != null && valueDataProperty != null)
                            {
                                var currentValueName = valueNameProperty.GetValue(value)?.ToString();
                                if (currentValueName != null && currentValueName.Equals(valueName, StringComparison.OrdinalIgnoreCase))
                                {
                                    return valueDataProperty.GetValue(value)?.ToString();
                                }
                            }
                        }
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reads uninstall registry key and creates Software objects
        /// </summary>
        /// <param name="hive">Registry hive</param>
        /// <param name="keyPath">Registry key path</param>
        /// <param name="softwareList">List to add software entries to</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        private void ReadUninstallKeyForSoftware(RegistryHiveOnDemand hive, string keyPath, List<Software> softwareList, PSCmdlet? cmdlet)
        {
            try
            {
                var uninstallKey = hive.GetKey(keyPath);
                if (uninstallKey == null) return;

                foreach (var subKey in uninstallKey.SubKeys)
                {
                    try
                    {
                        var displayName = GetStringValue(subKey, "DisplayName");

                        // Only include entries with a display name
                        if (!string.IsNullOrEmpty(displayName))
                        {
                            var registryKeyPath = $@"HKLM\SOFTWARE\{keyPath}\{subKey.KeyName}";

                            // Get raw values from registry
                            var displayVersionRaw = GetStringValue(subKey, "DisplayVersion");
                            var installDateRaw = GetStringValue(subKey, "InstallDate");
                            var publisherRaw = GetStringValue(subKey, "Publisher");

                            // Parse version - use parsed Version if successful, otherwise original string
                            object? displayVersion = null;
                            if (!string.IsNullOrEmpty(displayVersionRaw))
                            {
                                var parsedVersion = FormatUtilityService.ParseVersion(displayVersionRaw!);
                                if (parsedVersion != null)
                                {
                                    displayVersion = parsedVersion;
                                }
                                else
                                {
                                    displayVersion = displayVersionRaw;
                                }
                            }

                            // Parse install date - use parsed DateTime if successful, otherwise original string
                            object? installDate = null;
                            if (!string.IsNullOrEmpty(installDateRaw))
                            {
                                var parsedDate = FormatUtilityService.ParseDate(installDateRaw!);
                                if (parsedDate != null)
                                {
                                    installDate = parsedDate;
                                }
                                else
                                {
                                    installDate = installDateRaw;
                                }
                            }

                            var software = new Software
                            {
                                DisplayName = displayName!, // Already checked for null/empty above
                                Publisher = publisherRaw ?? string.Empty,
                                DisplayVersion = displayVersion,
                                InstallDate = installDate,
                                RegistryKeyPath = registryKeyPath
                            };

                            softwareList.Add(software);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName,
                            $"Error reading software entry {subKey.KeyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Error reading uninstall key {keyPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes the registry package service
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
