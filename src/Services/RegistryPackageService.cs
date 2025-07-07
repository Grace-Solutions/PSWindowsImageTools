using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Registry;

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
                        var key = $"VersionInfo.{value.ValueName}";
                        versionInfo[key] = value.ValueData;
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Successfully read {versionInfo.Count} version properties");
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

                // Read installed software
                var softwareInfo = ReadInstalledSoftware(mountPath, cmdlet);
                foreach (var item in softwareInfo)
                {
                    registryInfo[item.Key] = item.Value;
                }

                // Read Windows Update configuration
                var wuConfigInfo = ReadWindowsUpdateConfig(mountPath, cmdlet);
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
        /// Reads installed software from both regular and WOW64 uninstall keys
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing software information</returns>
        private Dictionary<string, object> ReadInstalledSoftware(string mountPath, PSCmdlet? cmdlet)
        {
            var softwareInfo = new Dictionary<string, object>();
            var softwareList = new List<Dictionary<string, object>>();

            try
            {
                var softwareHivePath = Path.Combine(mountPath, "Windows", "System32", "config", "SOFTWARE");
                
                if (!File.Exists(softwareHivePath))
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, 
                        $"SOFTWARE hive not found at: {softwareHivePath}");
                    return softwareInfo;
                }

                var hive = new RegistryHiveOnDemand(softwareHivePath);

                // Read from regular uninstall key
                ReadUninstallKey(hive, @"Microsoft\Windows\CurrentVersion\Uninstall", softwareList, cmdlet);

                // Read from WOW64 uninstall key
                ReadUninstallKey(hive, @"WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", softwareList, cmdlet);

                softwareInfo["Software"] = softwareList.ToArray();

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Found {softwareList.Count} installed software entries");
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, 
                    $"Failed to read installed software: {ex.Message}");
            }

            return softwareInfo;
        }

        /// <summary>
        /// Reads software entries from a specific uninstall registry key
        /// </summary>
        /// <param name="hive">Registry hive</param>
        /// <param name="uninstallKeyPath">Path to uninstall key</param>
        /// <param name="softwareList">List to add software entries to</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        private void ReadUninstallKey(RegistryHiveOnDemand hive, string uninstallKeyPath,
            List<Dictionary<string, object>> softwareList, PSCmdlet? cmdlet)
        {
            try
            {
                var uninstallKey = hive.GetKey(uninstallKeyPath);
                if (uninstallKey == null)
                {
                    return;
                }

                foreach (var subKey in uninstallKey.SubKeys)
                {
                    try
                    {
                        var displayName = GetStringValue(subKey, "DisplayName");
                        if (!string.IsNullOrEmpty(displayName))
                        {
                            var registryKeyPath = $@"HKLM\SOFTWARE\{uninstallKeyPath}\{subKey.KeyName}";

                            var software = new Dictionary<string, object>
                            {
                                ["DisplayName"] = displayName ?? string.Empty,
                                ["DisplayVersion"] = GetStringValue(subKey, "DisplayVersion") ?? string.Empty,
                                ["Publisher"] = GetStringValue(subKey, "Publisher") ?? string.Empty,
                                ["RegistryKeyPath"] = registryKeyPath
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
                    $"Error reading uninstall key {uninstallKeyPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads Windows Update configuration from registry
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing Windows Update configuration</returns>
        private Dictionary<string, object> ReadWindowsUpdateConfig(string mountPath, PSCmdlet? cmdlet)
        {
            var wuConfigInfo = new Dictionary<string, object>();

            try
            {
                var softwareHivePath = Path.Combine(mountPath, "Windows", "System32", "config", "SOFTWARE");
                
                if (!File.Exists(softwareHivePath))
                {
                    return wuConfigInfo;
                }

                var hive = new RegistryHiveOnDemand(softwareHivePath);
                var wuKey = hive.GetKey(@"Policies\Microsoft\Windows\WindowsUpdate");
                
                if (wuKey != null)
                {
                    foreach (var value in wuKey.Values)
                    {
                        var key = $"WUConfig.{value.ValueName}";
                        wuConfigInfo[key] = value.ValueData;
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Read {wuConfigInfo.Count} Windows Update configuration properties");
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
