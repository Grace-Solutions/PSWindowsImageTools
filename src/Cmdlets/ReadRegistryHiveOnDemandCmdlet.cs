using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Registry;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Reads registry data from offline hive files on demand
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "RegistryHiveOnDemand")]
    [OutputType(typeof(Dictionary<string, object>))]
    public class ReadRegistryHiveOnDemandCmdlet : PSCmdlet
    {
        /// <summary>
        /// Path to the registry hive file
        /// </summary>
        [Parameter(
            Mandatory = true,
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Path to the registry hive file")]
        [ValidateNotNull]
        public FileInfo Path { get; set; } = null!;

        /// <summary>
        /// Registry key paths to read (optional, reads entire hive if not specified)
        /// </summary>
        [Parameter(
            Mandatory = false,
            Position = 1,
            HelpMessage = "Registry key paths to read (e.g., 'Microsoft\\Windows\\CurrentVersion')")]
        public string[]? KeyPath { get; set; }

        /// <summary>
        /// Maximum depth to recurse into subkeys (default: 1, 0 = no recursion, -1 = unlimited)
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Maximum depth to recurse into subkeys (default: 1, 0 = no recursion, -1 = unlimited)")]
        public int MaxDepth { get; set; } = 1;

        protected override void ProcessRecord()
        {
            try
            {
                // Check if file exists
                if (!Path.Exists)
                {
                    var error = new ErrorRecord(
                        new FileNotFoundException($"Registry hive file not found: {Path.FullName}"),
                        "HiveFileNotFound",
                        ErrorCategory.ObjectNotFound,
                        Path);
                    WriteError(error);
                    return;
                }

                var result = new Dictionary<string, object>();
                var hiveName = System.IO.Path.GetFileName(Path.FullName);

                using var registryService = new RegistryPackageService();

                if (KeyPath != null && KeyPath.Length > 0)
                {
                    WriteVerbose($"Reading custom key paths: {string.Join(", ", KeyPath)}");
                    // For custom key paths, use the raw hive reading
                    result = ReadCustomKeyPaths(Path.FullName, KeyPath);
                }
                else
                {
                    // Default: try to read common information based on hive name
                    WriteVerbose("Auto-detecting hive type and reading common information");
                    if (hiveName.Equals("SOFTWARE", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteVerbose("Reading Windows version information");
                        var versionInfo = registryService.ReadWindowsVersionInfoFromHive(Path.FullName, this);
                        foreach (var kvp in versionInfo)
                        {
                            result[kvp.Key] = kvp.Value;
                        }

                        WriteVerbose("Reading installed software information");
                        var softwareList = registryService.GetInstalledSoftwareFromHive(Path.FullName, this);
                        result["Software"] = softwareList;

                        WriteVerbose("Reading Windows Update configuration");
                        var wuConfig = registryService.ReadWindowsUpdateConfigurationFromHive(Path.FullName, this);
                        foreach (var kvp in wuConfig)
                        {
                            result[kvp.Key] = kvp.Value;
                        }
                    }
                    else
                    {
                        WriteWarning($"Unknown hive type '{hiveName}'. Use -KeyPath to specify a specific registry path to read.");
                    }
                }

                WriteObject(result);
            }
            catch (Exception ex)
            {
                var error = new ErrorRecord(
                    ex,
                    "RegistryHiveError",
                    ErrorCategory.ReadError,
                    Path);
                WriteError(error);
            }
        }

        private void ReadRegistryKey(RegistryHiveOnDemand hive, object? key, string currentPath, Dictionary<string, object> result, int currentDepth)
        {
            if (key == null || (MaxDepth >= 0 && currentDepth > MaxDepth))
                return;

            try
            {
                // Create key data structure
                var keyData = new Dictionary<string, object>();
                keyData["KeyPath"] = currentPath;

                // Read values in current key using reflection to access Values property
                var values = new Dictionary<string, object>();
                var valuesProperty = key.GetType().GetProperty("Values");
                if (valuesProperty != null)
                {
                    var valuesCollection = valuesProperty.GetValue(key);
                    if (valuesCollection != null)
                    {
                        foreach (var value in (System.Collections.IEnumerable)valuesCollection)
                        {
                            try
                            {
                                var valueNameProp = value.GetType().GetProperty("ValueName");
                                var valueDataProp = value.GetType().GetProperty("ValueData");

                                var valueName = valueNameProp?.GetValue(value) as string;
                                var valueData = valueDataProp?.GetValue(value);

                                if (!string.IsNullOrEmpty(valueName) && valueData != null)
                                {
                                    values[valueName!] = valueData;
                                }
                            }
                            catch (Exception ex)
                            {
                                WriteVerbose($"Error reading value: {ex.Message}");
                            }
                        }
                    }
                }
                keyData["Values"] = values;

                // Use the leaf name (last part of the path) as the dictionary key
                var leafName = string.IsNullOrEmpty(currentPath) ? "Root" :
                              currentPath.Contains("\\") ? currentPath.Substring(currentPath.LastIndexOf("\\") + 1) : currentPath;

                // Handle duplicate leaf names by appending the full path
                var resultKey = leafName;
                var counter = 1;
                while (result.ContainsKey(resultKey))
                {
                    resultKey = $"{leafName}_{counter}";
                    counter++;
                }

                result[resultKey] = keyData;

                // Read subkeys if depth allows
                if (MaxDepth < 0 || currentDepth < MaxDepth)
                {
                    var subKeysProperty = key.GetType().GetProperty("SubKeys");
                    if (subKeysProperty != null)
                    {
                        var subKeysCollection = subKeysProperty.GetValue(key);
                        if (subKeysCollection != null)
                        {
                            foreach (var subKey in (System.Collections.IEnumerable)subKeysCollection)
                            {
                                try
                                {
                                    var keyNameProp = subKey.GetType().GetProperty("KeyName");
                                    var keyName = keyNameProp?.GetValue(subKey) as string;

                                    if (!string.IsNullOrEmpty(keyName))
                                    {
                                        var subKeyPath = string.IsNullOrEmpty(currentPath) ? keyName! : $"{currentPath}\\{keyName}";
                                        ReadRegistryKey(hive, subKey, subKeyPath, result, currentDepth + 1);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    WriteVerbose($"Error reading subkey: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteVerbose($"Error reading key {currentPath}: {ex.Message}");
            }
        }

        private Dictionary<string, object> ReadCustomKeyPaths(string hivePath, string[] keyPaths)
        {
            var result = new Dictionary<string, object>();

            try
            {
                var hive = new RegistryHiveOnDemand(hivePath);

                foreach (var keyPath in keyPaths)
                {
                    if (string.IsNullOrEmpty(keyPath)) continue;

                    var key = hive.GetKey(keyPath);
                    if (key == null)
                    {
                        WriteWarning($"Registry key not found: {keyPath}");
                        continue;
                    }

                    ReadRegistryKey(hive, key, keyPath, result, 0);
                }

                // Force cleanup
                hive = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                WriteWarning($"Error reading key paths: {ex.Message}");
            }

            return result;
        }
    }
}
