using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Registry;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for reading offline registry using Registry package
    /// No hive mounting required - clean and efficient approach
    /// </summary>
    public class RegistryPackageService : IDisposable
    {
        private const string ServiceName = "RegistryPackageService";
        private bool _disposed = false;

        /// <summary>
        /// Reads only essential Windows version information from the SOFTWARE hive
        /// This approach focuses only on the CurrentVersion registry key
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing Windows version information</returns>
        public Dictionary<string, object> ReadWindowsVersionOnly(string mountPath, PSCmdlet? cmdlet = null)
        {
            var registryInfo = new Dictionary<string, object>();

            try
            {
                var hives = GetRegistryHivePaths(mountPath);

                // Only read SOFTWARE hive for Windows version information
                if (File.Exists(hives["SOFTWARE"]))
                {
                    var versionInfo = ReadWindowsCurrentVersion(hives["SOFTWARE"], cmdlet);
                    foreach (var item in versionInfo)
                    {
                        registryInfo[item.Key] = item.Value;
                    }
                }
                else
                {
                    registryInfo["SOFTWAREHiveExists"] = false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName,
                    $"Failed to read Windows version info: {ex.Message}");

                registryInfo["RegistryReadError"] = ex.Message;
            }

            return registryInfo;
        }

        /// <summary>
        /// Reads comprehensive registry information from offline Windows image using Registry package
        /// This approach does NOT require mounting registry hives
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing registry information</returns>
        public Dictionary<string, object> ReadOfflineRegistry(string mountPath, PSCmdlet? cmdlet = null)
        {
            var registryInfo = new Dictionary<string, object>();

            try
            {
                // Reading offline registry using Registry package

                // Define registry hive paths
                var hives = GetRegistryHivePaths(mountPath);

                // Check hive file existence and basic info
                foreach (var hive in hives)
                {
                    if (File.Exists(hive.Value))
                    {
                        var fileInfo = new FileInfo(hive.Value);
                        registryInfo[$"{hive.Key}HiveExists"] = true;
                        registryInfo[$"{hive.Key}HiveSize"] = fileInfo.Length;
                        registryInfo[$"{hive.Key}HiveLastModified"] = fileInfo.LastWriteTime;
                        
                        // Found hive file
                    }
                    else
                    {
                        registryInfo[$"{hive.Key}HiveExists"] = false;
                        // Missing hive file
                    }
                }

                // Read SOFTWARE hive information
                if (File.Exists(hives["SOFTWARE"]))
                {
                    var softwareInfo = ReadSoftwareHive(hives["SOFTWARE"], cmdlet);
                    foreach (var item in softwareInfo)
                    {
                        registryInfo[item.Key] = item.Value;
                    }
                }

                // Read SYSTEM hive information
                if (File.Exists(hives["SYSTEM"]))
                {
                    var systemInfo = ReadSystemHive(hives["SYSTEM"], cmdlet);
                    foreach (var item in systemInfo)
                    {
                        registryInfo[item.Key] = item.Value;
                    }
                }

                // Successfully read registry values
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, 
                    $"Failed to read offline registry: {ex.Message}");
                
                registryInfo["RegistryReadError"] = ex.Message;
            }

            return registryInfo;
        }

        /// <summary>
        /// Gets registry hive file paths for a mounted Windows image
        /// </summary>
        /// <param name="mountPath">Path where the Windows image is mounted</param>
        /// <returns>Dictionary of hive names and their file paths</returns>
        private static Dictionary<string, string> GetRegistryHivePaths(string mountPath)
        {
            var configPath = Path.Combine(mountPath, "Windows", "System32", "config");
            
            return new Dictionary<string, string>
            {
                ["SYSTEM"] = Path.Combine(configPath, "SYSTEM"),
                ["SOFTWARE"] = Path.Combine(configPath, "SOFTWARE"),
                ["SECURITY"] = Path.Combine(configPath, "SECURITY"),
                ["SAM"] = Path.Combine(configPath, "SAM"),
                ["DEFAULT"] = Path.Combine(configPath, "DEFAULT")
            };
        }

        /// <summary>
        /// Reads SOFTWARE hive information using Registry package
        /// </summary>
        /// <param name="softwareHivePath">Path to SOFTWARE hive file</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary of SOFTWARE hive information</returns>
        private Dictionary<string, object> ReadSoftwareHive(string softwareHivePath, PSCmdlet? cmdlet)
        {
            var softwareInfo = new Dictionary<string, object>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    "Reading SOFTWARE hive using Registry package");

                var softwareHive = new RegistryHive(softwareHivePath);
                softwareHive.ParseHive();

                // Read Windows version information
                var versionInfo = ReadWindowsVersionInfo(softwareHive, cmdlet);
                foreach (var item in versionInfo)
                {
                    softwareInfo[item.Key] = item.Value;
                }

                // Read installed programs information
                var programsInfo = ReadInstalledProgramsInfo(softwareHive, cmdlet);
                foreach (var item in programsInfo)
                {
                    softwareInfo[item.Key] = item.Value;
                }

                // Read Windows Update information
                var updateInfo = ReadWindowsUpdateInfo(softwareHive, cmdlet);
                foreach (var item in updateInfo)
                {
                    softwareInfo[item.Key] = item.Value;
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Read {softwareInfo.Count} SOFTWARE hive values");
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, 
                    $"Error reading SOFTWARE hive: {ex.Message}");
                softwareInfo["SoftwareHiveError"] = ex.Message;
            }

            return softwareInfo;
        }

        /// <summary>
        /// Reads SYSTEM hive information using Registry package
        /// </summary>
        /// <param name="systemHivePath">Path to SYSTEM hive file</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary of SYSTEM hive information</returns>
        private Dictionary<string, object> ReadSystemHive(string systemHivePath, PSCmdlet? cmdlet)
        {
            var systemInfo = new Dictionary<string, object>();

            try
            {
                // Reading SYSTEM hive

                var systemHive = new RegistryHive(systemHivePath);
                systemHive.ParseHive();

                // Read computer information
                var computerInfo = ReadComputerInfo(systemHive, cmdlet);
                foreach (var item in computerInfo)
                {
                    systemInfo[item.Key] = item.Value;
                }

                // Read services information
                var servicesInfo = ReadServicesInfo(systemHive, cmdlet);
                foreach (var item in servicesInfo)
                {
                    systemInfo[item.Key] = item.Value;
                }

                // Read timezone information
                var timezoneInfo = ReadTimezoneInfo(systemHive, cmdlet);
                foreach (var item in timezoneInfo)
                {
                    systemInfo[item.Key] = item.Value;
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Read {systemInfo.Count} SYSTEM hive values");
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, 
                    $"Error reading SYSTEM hive: {ex.Message}");
                systemInfo["SystemHiveError"] = ex.Message;
            }

            return systemInfo;
        }

        /// <summary>
        /// Reads only Windows CurrentVersion information from SOFTWARE hive file
        /// This is a simplified version that only reads essential version data
        /// </summary>
        /// <param name="softwareHivePath">Path to SOFTWARE hive file</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Dictionary containing Windows version information</returns>
        private Dictionary<string, object> ReadWindowsCurrentVersion(string softwareHivePath, PSCmdlet? cmdlet)
        {
            var versionInfo = new Dictionary<string, object>();

            try
            {
                var softwareHive = new RegistryHive(softwareHivePath);
                softwareHive.ParseHive();

                var versionKey = softwareHive.GetKey(@"Microsoft\Windows NT\CurrentVersion");
                if (versionKey != null)
                {
                    // Only read essential version values
                    foreach (var keyValue in versionKey.Values)
                    {
                        if (!string.IsNullOrEmpty(keyValue.ValueName) &&
                            keyValue.ValueData != null &&
                            !string.IsNullOrEmpty(keyValue.ValueData.ToString()))
                        {
                            versionInfo[keyValue.ValueName] = keyValue.ValueData;
                        }
                    }
                }
                else
                {
                    versionInfo["CurrentVersionKeyNotFound"] = true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName,
                    $"Error reading SOFTWARE hive: {ex.Message}");
                versionInfo["RegistryReadError"] = ex.Message;
            }

            return versionInfo;
        }

        /// <summary>
        /// Reads Windows version information from SOFTWARE hive
        /// Dynamically enumerates all values from Microsoft\Windows NT\CurrentVersion
        /// </summary>
        private Dictionary<string, object> ReadWindowsVersionInfo(RegistryHive softwareHive, PSCmdlet? cmdlet)
        {
            var versionInfo = new Dictionary<string, object>();

            try
            {
                var versionKey = softwareHive.GetKey(@"Microsoft\Windows NT\CurrentVersion");
                if (versionKey != null)
                {
                    // Dynamically enumerate all values from the CurrentVersion key
                    foreach (var keyValue in versionKey.Values)
                    {
                        if (!string.IsNullOrEmpty(keyValue.ValueName) &&
                            keyValue.ValueData != null &&
                            !string.IsNullOrEmpty(keyValue.ValueData.ToString()))
                        {
                            versionInfo[keyValue.ValueName] = keyValue.ValueData;
                        }
                    }

                    LoggingService.WriteVerbose(cmdlet, ServiceName,
                        $"Read {versionInfo.Count} values from CurrentVersion key");
                }
                else
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName,
                        "CurrentVersion registry key not found");
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName,
                    $"Error reading Windows version info: {ex.Message}");
            }

            return versionInfo;
        }

        /// <summary>
        /// Reads installed programs information from SOFTWARE hive
        /// Scans both regular and WOW6432Node uninstall keys for complete software detection
        /// </summary>
        private Dictionary<string, object> ReadInstalledProgramsInfo(RegistryHive softwareHive, PSCmdlet? cmdlet)
        {
            var programsInfo = new Dictionary<string, object>();
            int totalProgramCount = 0;

            try
            {
                // Define both uninstall key paths to scan
                var uninstallPaths = new[]
                {
                    @"Microsoft\Windows\CurrentVersion\Uninstall",           // 64-bit and native programs
                    @"WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" // 32-bit programs on 64-bit systems
                };

                foreach (var uninstallPath in uninstallPaths)
                {
                    try
                    {
                        var uninstallKey = softwareHive.GetKey(uninstallPath);
                        if (uninstallKey != null)
                        {
                            var programCount = uninstallKey.SubKeys.Count;
                            totalProgramCount += programCount;

                            LoggingService.WriteVerbose(cmdlet, ServiceName,
                                $"Found {programCount} programs in {uninstallPath}");
                        }
                        else
                        {
                            LoggingService.WriteVerbose(cmdlet, ServiceName,
                                $"Uninstall key not found: {uninstallPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName,
                            $"Error reading {uninstallPath}: {ex.Message}");
                    }
                }

                // Set the combined results
                programsInfo["InstalledProgramCount"] = totalProgramCount;
                programsInfo["InstalledProgramCount64Bit"] = GetProgramCountFromPath(softwareHive, @"Microsoft\Windows\CurrentVersion\Uninstall");
                programsInfo["InstalledProgramCount32Bit"] = GetProgramCountFromPath(softwareHive, @"WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Found {totalProgramCount} total installed programs ({programsInfo["InstalledProgramCount64Bit"]} 64-bit, {programsInfo["InstalledProgramCount32Bit"]} 32-bit)");
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName,
                    $"Error reading installed programs: {ex.Message}");
            }

            return programsInfo;
        }

        /// <summary>
        /// Helper method to get program count from a specific uninstall path
        /// </summary>
        private int GetProgramCountFromPath(RegistryHive softwareHive, string uninstallPath)
        {
            try
            {
                var uninstallKey = softwareHive.GetKey(uninstallPath);
                return uninstallKey?.SubKeys.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Reads Windows Update information from SOFTWARE hive
        /// </summary>
        private Dictionary<string, object> ReadWindowsUpdateInfo(RegistryHive softwareHive, PSCmdlet? cmdlet)
        {
            var updateInfo = new Dictionary<string, object>();

            try
            {
                var wuKey = softwareHive.GetKey(@"Microsoft\Windows\CurrentVersion\WindowsUpdate");
                if (wuKey != null)
                {
                    var auKey = wuKey.SubKeys.FirstOrDefault(sk => sk.KeyName == "Auto Update");
                    if (auKey != null)
                    {
                        var auOptionsValue = auKey.Values.FirstOrDefault(v => v.ValueName == "AUOptions");
                        if (auOptionsValue != null)
                        {
                            updateInfo["WindowsUpdateAUOptions"] = auOptionsValue.ValueData;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, 
                    $"Error reading Windows Update info: {ex.Message}");
            }

            return updateInfo;
        }

        /// <summary>
        /// Reads computer information from SYSTEM hive
        /// </summary>
        private Dictionary<string, object> ReadComputerInfo(RegistryHive systemHive, PSCmdlet? cmdlet)
        {
            var computerInfo = new Dictionary<string, object>();

            try
            {
                var computerNameKey = systemHive.GetKey(@"ControlSet001\Control\ComputerName\ComputerName");
                if (computerNameKey != null)
                {
                    var computerNameValue = computerNameKey.Values.FirstOrDefault(v => v.ValueName == "ComputerName");
                    if (computerNameValue != null && !string.IsNullOrEmpty(computerNameValue.ValueData))
                    {
                        computerInfo["ComputerName"] = computerNameValue.ValueData;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, 
                    $"Error reading computer info: {ex.Message}");
            }

            return computerInfo;
        }

        /// <summary>
        /// Reads services information from SYSTEM hive
        /// </summary>
        private Dictionary<string, object> ReadServicesInfo(RegistryHive systemHive, PSCmdlet? cmdlet)
        {
            var servicesInfo = new Dictionary<string, object>();

            try
            {
                var servicesKey = systemHive.GetKey(@"ControlSet001\Services");
                if (servicesKey != null)
                {
                    var serviceCount = servicesKey.SubKeys.Count;
                    servicesInfo["ServiceCount"] = serviceCount;

                    var sampleServices = servicesKey.SubKeys
                        .Take(10)
                        .Select(sk => sk.KeyName)
                        .ToList();

                    if (sampleServices.Any())
                    {
                        servicesInfo["SampleServices"] = sampleServices;
                    }

                    LoggingService.WriteVerbose(cmdlet, ServiceName, 
                        $"Found {serviceCount} services");
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, 
                    $"Error reading services info: {ex.Message}");
            }

            return servicesInfo;
        }

        /// <summary>
        /// Reads timezone information from SYSTEM hive
        /// </summary>
        private Dictionary<string, object> ReadTimezoneInfo(RegistryHive systemHive, PSCmdlet? cmdlet)
        {
            var timezoneInfo = new Dictionary<string, object>();

            try
            {
                var timezoneKey = systemHive.GetKey(@"ControlSet001\Control\TimeZoneInformation");
                if (timezoneKey != null)
                {
                    var timezoneValue = timezoneKey.Values.FirstOrDefault(v => v.ValueName == "TimeZoneKeyName");
                    if (timezoneValue != null && !string.IsNullOrEmpty(timezoneValue.ValueData))
                    {
                        timezoneInfo["TimeZone"] = timezoneValue.ValueData;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, 
                    $"Error reading timezone info: {ex.Message}");
            }

            return timezoneInfo;
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
