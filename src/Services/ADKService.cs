using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for detecting Windows ADK installations and managing Optional Components
    /// </summary>
    public class ADKService
    {
        private const string ServiceName = "ADKService";

        /// <summary>
        /// Detects installed Windows ADK installations on the system
        /// </summary>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>List of detected ADK installations</returns>
        public List<ADKInfo> DetectADKInstallations(PSCmdlet? cmdlet = null)
        {
            var adkInstallations = new List<ADKInfo>();

            LoggingService.WriteVerbose(cmdlet, ServiceName, "Starting ADK detection");

            try
            {
                // Check both 32-bit and 64-bit registry locations
                var registryPaths = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var registryPath in registryPaths)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Scanning registry path: {registryPath}");
                    
                    using var baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryPath);
                    if (baseKey == null) continue;

                    foreach (var subKeyName in baseKey.GetSubKeyNames())
                    {
                        using var subKey = baseKey.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var displayName = subKey.GetValue("DisplayName")?.ToString() ?? "";
                        
                        // Look for ADK installations
                        if (IsADKInstallation(displayName))
                        {
                            var adkInfo = ParseADKRegistryEntry(subKey, registryPath + "\\" + subKeyName, cmdlet);
                            if (adkInfo != null)
                            {
                                adkInstallations.Add(adkInfo);
                                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                                    $"Found ADK installation: {adkInfo.DisplayName} at {adkInfo.InstallationPath?.FullName}");
                            }
                        }
                    }
                }

                // Remove duplicates and validate installations
                adkInstallations = adkInstallations
                    .GroupBy(adk => adk.InstallationPath?.FullName)
                    .Select(g => g.First())
                    .Where(adk => ValidateADKInstallation(adk, cmdlet))
                    .OrderByDescending(adk => adk.Version)
                    .ToList();

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Detected {adkInstallations.Count} valid ADK installations");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, "Failed to detect ADK installations", ex);
            }

            return adkInstallations;
        }

        /// <summary>
        /// Gets available WinPE Optional Components from an ADK installation
        /// </summary>
        /// <param name="adkInfo">ADK installation information</param>
        /// <param name="architecture">Target architecture (x86, amd64, arm64)</param>
        /// <param name="includeLanguagePacks">Whether to include language packs</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>List of available optional components</returns>
        public List<WinPEOptionalComponent> GetAvailableOptionalComponents(
            ADKInfo adkInfo, 
            string architecture = "amd64", 
            bool includeLanguagePacks = false,
            PSCmdlet? cmdlet = null)
        {
            var components = new List<WinPEOptionalComponent>();

            LoggingService.WriteVerbose(cmdlet, ServiceName, 
                $"Scanning for {architecture} Optional Components in ADK at {adkInfo.InstallationPath?.FullName}");

            try
            {
                if (adkInfo.WinPEOptionalComponentsPath == null || !adkInfo.WinPEOptionalComponentsPath.Exists)
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, "WinPE Optional Components path not found or inaccessible");
                    return components;
                }

                var archPath = Path.Combine(adkInfo.WinPEOptionalComponentsPath.FullName, architecture, "WinPE_OCs");
                if (!Directory.Exists(archPath))
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, $"Architecture path not found: {archPath}");
                    return components;
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Scanning directory: {archPath}");

                var cabFiles = Directory.GetFiles(archPath, "*.cab", SearchOption.TopDirectoryOnly);
                var totalFiles = cabFiles.Length;

                for (int i = 0; i < cabFiles.Length; i++)
                {
                    var cabFile = cabFiles[i];
                    var fileName = Path.GetFileNameWithoutExtension(cabFile);
                    
                    var progress = (int)((double)(i + 1) / totalFiles * 100);
                    LoggingService.WriteProgress(cmdlet, "Scanning Optional Components",
                        $"[{i + 1} of {totalFiles}] - {fileName}",
                        $"Processing {fileName} ({progress}%)", progress);

                    try
                    {
                        var component = ParseOptionalComponent(cabFile, architecture, cmdlet);
                        if (component != null)
                        {
                            // Filter language packs if not requested
                            if (!includeLanguagePacks && component.IsLanguagePack)
                            {
                                continue;
                            }

                            components.Add(component);
                            LoggingService.WriteVerbose(cmdlet, ServiceName, 
                                $"Found component: {component.Name} ({component.SizeFormatted})");
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName, 
                            $"Failed to parse component {fileName}: {ex.Message}");
                    }
                }

                // Find language packs for components
                if (includeLanguagePacks)
                {
                    FindLanguagePacksForComponents(components, archPath, cmdlet);
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Found {components.Count} optional components for {architecture}");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, "Failed to scan for optional components", ex);
            }

            return components.OrderBy(c => c.Category).ThenBy(c => c.Name).ToList();
        }

        /// <summary>
        /// Determines if a registry entry represents an ADK installation
        /// </summary>
        private bool IsADKInstallation(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return false;

            var adkPatterns = new[]
            {
                @"Windows Assessment and Deployment Kit",
                @"Windows ADK",
                @"Microsoft Windows Assessment and Deployment Kit",
                @"Windows Kits.*Assessment.*Deployment"
            };

            return adkPatterns.Any(pattern => Regex.IsMatch(displayName, pattern, RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Parses ADK information from a registry entry
        /// </summary>
        private ADKInfo? ParseADKRegistryEntry(RegistryKey registryKey, string keyPath, PSCmdlet? cmdlet)
        {
            try
            {
                var displayName = registryKey.GetValue("DisplayName")?.ToString() ?? "";
                var installLocation = registryKey.GetValue("InstallLocation")?.ToString();
                var versionString = registryKey.GetValue("DisplayVersion")?.ToString();
                var publisher = registryKey.GetValue("Publisher")?.ToString() ?? "";
                var installDateString = registryKey.GetValue("InstallDate")?.ToString();

                if (string.IsNullOrEmpty(installLocation) || !Directory.Exists(installLocation))
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, 
                        $"Skipping ADK entry - invalid install location: {installLocation}");
                    return null;
                }

                var adkInfo = new ADKInfo
                {
                    DisplayName = displayName,
                    InstallationPath = new DirectoryInfo(installLocation),
                    Publisher = publisher,
                    RegistryKey = keyPath
                };

                // Parse version
                if (!string.IsNullOrEmpty(versionString) && Version.TryParse(versionString, out var version))
                {
                    adkInfo.Version = version;
                }

                // Parse install date
                if (!string.IsNullOrEmpty(installDateString) && 
                    DateTime.TryParseExact(installDateString, "yyyyMMdd", null, 
                        System.Globalization.DateTimeStyles.None, out var installDate))
                {
                    adkInfo.InstallDate = installDate;
                }

                return adkInfo;
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, 
                    $"Failed to parse ADK registry entry: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Validates and enriches ADK installation information
        /// </summary>
        private bool ValidateADKInstallation(ADKInfo adkInfo, PSCmdlet? cmdlet)
        {
            try
            {
                if (adkInfo.InstallationPath == null || !adkInfo.InstallationPath.Exists)
                {
                    return false;
                }

                var installPath = adkInfo.InstallationPath.FullName;

                // Check for WinPE Optional Components
                var winpeOCPath = Path.Combine(installPath, "Assessment and Deployment Kit", "Windows Preinstallation Environment");
                if (Directory.Exists(winpeOCPath))
                {
                    adkInfo.HasWinPEAddon = true;
                    adkInfo.WinPEOptionalComponentsPath = new DirectoryInfo(winpeOCPath);
                }

                // Check for Deployment Tools
                var deploymentToolsPath = Path.Combine(installPath, "Assessment and Deployment Kit", "Deployment Tools");
                if (Directory.Exists(deploymentToolsPath))
                {
                    adkInfo.HasDeploymentTools = true;
                    
                    // Look for DISM
                    var dismPath = Path.Combine(deploymentToolsPath, "DISM", "dism.exe");
                    if (File.Exists(dismPath))
                    {
                        adkInfo.DismPath = new FileInfo(dismPath);
                    }
                }

                // Check for supported architectures
                if (adkInfo.WinPEOptionalComponentsPath != null)
                {
                    var archDirs = new[] { "x86", "amd64", "arm64" };
                    foreach (var arch in archDirs)
                    {
                        var archPath = Path.Combine(adkInfo.WinPEOptionalComponentsPath.FullName, arch);
                        if (Directory.Exists(archPath))
                        {
                            adkInfo.SupportedArchitectures.Add(arch);
                        }
                    }
                }

                // Must have at least WinPE addon or Deployment Tools
                return adkInfo.HasWinPEAddon || adkInfo.HasDeploymentTools;
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, 
                    $"Failed to validate ADK installation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parses an Optional Component CAB file to extract metadata
        /// </summary>
        private WinPEOptionalComponent? ParseOptionalComponent(string cabFilePath, string architecture, PSCmdlet? cmdlet)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(cabFilePath);
                var fileInfo = new FileInfo(cabFilePath);

                var component = new WinPEOptionalComponent
                {
                    ComponentFile = fileInfo,
                    Architecture = architecture,
                    SizeInBytes = fileInfo.Length
                };

                // Parse component name and type from filename
                if (fileName.StartsWith("WinPE-", StringComparison.OrdinalIgnoreCase))
                {
                    // Standard WinPE component (e.g., WinPE-NetFx-Package.cab)
                    var namePart = fileName.Substring(6); // Remove "WinPE-" prefix

                    if (namePart.EndsWith("-Package", StringComparison.OrdinalIgnoreCase))
                    {
                        component.Name = namePart.Substring(0, namePart.Length - 8); // Remove "-Package" suffix
                        component.DisplayName = $"WinPE {component.Name}";
                        component.Category = DetermineComponentCategory(component.Name);
                        component.Description = GenerateComponentDescription(component.Name);
                    }
                    else
                    {
                        // Language pack or other variant
                        component.Name = namePart;
                        component.DisplayName = $"WinPE {namePart}";

                        // Check if it's a language pack
                        var langMatch = Regex.Match(namePart, @"([a-z]{2}-[a-z]{2})$", RegexOptions.IgnoreCase);
                        if (langMatch.Success)
                        {
                            component.IsLanguagePack = true;
                            component.LanguageCode = langMatch.Groups[1].Value.ToLowerInvariant();
                            component.Category = "Language Packs";
                            component.Description = $"Language pack for {component.LanguageCode}";
                        }
                        else
                        {
                            component.Category = "Other";
                        }
                    }
                }
                else
                {
                    // Non-standard component
                    component.Name = fileName;
                    component.DisplayName = fileName;
                    component.Category = "Other";
                    component.Description = "Optional component";
                }

                return component;
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName,
                    $"Failed to parse optional component {cabFilePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Determines the category of a component based on its name
        /// </summary>
        private string DetermineComponentCategory(string componentName)
        {
            var categoryMappings = new Dictionary<string, string[]>
            {
                ["Networking"] = new[] { "NetFx", "RNDIS", "WDS-Tools", "WiFi-Direct", "Dot3Svc" },
                ["Storage"] = new[] { "StorageWMI", "FMAPI", "SecureBootCmdlets" },
                ["Scripting"] = new[] { "PowerShell", "Scripting", "WMI" },
                ["Security"] = new[] { "SecureStartup", "FIPS", "EnhancedStorage" },
                ["Hardware"] = new[] { "HTA", "PlatformId", "Setup" },
                ["Development"] = new[] { "DismCmdlets", "PmemCmdlets" },
                ["Fonts"] = new[] { "FontSupport" },
                ["Language"] = new[] { "LanguagePack", "Speech" }
            };

            foreach (var category in categoryMappings)
            {
                if (category.Value.Any(keyword =>
                    componentName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return category.Key;
                }
            }

            return "General";
        }

        /// <summary>
        /// Generates a description for a component based on its name
        /// </summary>
        private string GenerateComponentDescription(string componentName)
        {
            var descriptions = new Dictionary<string, string>
            {
                ["NetFx"] = ".NET Framework support for WinPE",
                ["PowerShell"] = "Windows PowerShell support for WinPE",
                ["StorageWMI"] = "Storage management WMI providers",
                ["RNDIS"] = "Remote Network Driver Interface Specification support",
                ["WDS-Tools"] = "Windows Deployment Services client tools",
                ["FMAPI"] = "File Management API support",
                ["SecureStartup"] = "BitLocker and secure startup support",
                ["HTA"] = "HTML Application support",
                ["Scripting"] = "Windows Script Host support",
                ["WMI"] = "Windows Management Instrumentation support",
                ["DismCmdlets"] = "DISM PowerShell cmdlets",
                ["FontSupport"] = "Additional font support for international languages"
            };

            return descriptions.TryGetValue(componentName, out var description)
                ? description
                : $"WinPE optional component: {componentName}";
        }

        /// <summary>
        /// Finds language packs for components
        /// </summary>
        private void FindLanguagePacksForComponents(List<WinPEOptionalComponent> components, string archPath, PSCmdlet? cmdlet)
        {
            try
            {
                // Look for language subdirectories
                var langDirs = Directory.GetDirectories(archPath)
                    .Where(d => Regex.IsMatch(Path.GetFileName(d), @"^[a-z]{2}-[a-z]{2}$", RegexOptions.IgnoreCase))
                    .ToArray();

                foreach (var component in components.Where(c => !c.IsLanguagePack))
                {
                    foreach (var langDir in langDirs)
                    {
                        var langCode = Path.GetFileName(langDir);
                        var expectedLangPackName = $"WinPE-{component.Name}_{langCode}.cab";
                        var langPackPath = Path.Combine(langDir, expectedLangPackName);

                        if (File.Exists(langPackPath))
                        {
                            component.LanguagePackFiles.Add(new FileInfo(langPackPath));
                            LoggingService.WriteVerbose(cmdlet, ServiceName,
                                $"Found language pack for {component.Name}: {langCode}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName,
                    $"Failed to find language packs: {ex.Message}");
            }
        }
    }
}
