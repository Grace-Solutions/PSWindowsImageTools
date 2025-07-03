using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Newtonsoft.Json;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for managing Windows Autopilot configurations
    /// </summary>
    public class AutopilotService
    {
        private const string ServiceName = "AutopilotService";

        /// <summary>
        /// Loads Autopilot configuration from JSON file
        /// </summary>
        /// <param name="filePath">Path to the JSON file</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Autopilot configuration</returns>
        public AutopilotConfiguration LoadConfiguration(string filePath, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Loading Autopilot configuration from {filePath}");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Autopilot configuration file not found: {filePath}");
                }

                var json = File.ReadAllText(filePath);
                var config = AutopilotConfiguration.FromJson(json);
                config.SourceFilePath = filePath;

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Successfully loaded Autopilot configuration for tenant: {config.CloudAssignedTenantDomain}");

                return config;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    $"Failed to load Autopilot configuration from {filePath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Loads multiple Autopilot configurations from a directory
        /// </summary>
        /// <param name="directoryPath">Path to directory containing JSON files</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>List of Autopilot configurations</returns>
        public List<AutopilotConfiguration> LoadConfigurations(string directoryPath, PSCmdlet? cmdlet = null)
        {
            var configurations = new List<AutopilotConfiguration>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Loading Autopilot configurations from directory: {directoryPath}");

                if (!Directory.Exists(directoryPath))
                {
                    throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
                }

                var jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
                
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Found {jsonFiles.Length} JSON files to process");

                foreach (var file in jsonFiles)
                {
                    try
                    {
                        var config = LoadConfiguration(file, cmdlet);
                        configurations.Add(config);
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName,
                            $"Failed to load {file}: {ex.Message}");
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Successfully loaded {configurations.Count} Autopilot configurations");

                return configurations;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    $"Failed to load Autopilot configurations from {directoryPath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Saves Autopilot configuration to JSON file
        /// </summary>
        /// <param name="configuration">Configuration to save</param>
        /// <param name="filePath">Output file path</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        public void SaveConfiguration(AutopilotConfiguration configuration, string filePath, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Saving Autopilot configuration to {filePath}");

                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = configuration.ToJson(true);
                File.WriteAllText(filePath, json);

                configuration.SourceFilePath = filePath;
                configuration.IsModified = false;

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Successfully saved Autopilot configuration to {filePath}");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    $"Failed to save Autopilot configuration to {filePath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Applies Autopilot configuration to mounted Windows images
        /// </summary>
        /// <param name="mountedImages">Mounted Windows images</param>
        /// <param name="configuration">Autopilot configuration to apply</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>List of application results</returns>
        public List<AutopilotApplicationResult> ApplyConfiguration(
            MountedWindowsImage[] mountedImages,
            AutopilotConfiguration configuration,
            PSCmdlet? cmdlet = null)
        {
            var results = new List<AutopilotApplicationResult>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Applying Autopilot configuration to {mountedImages.Length} mounted images");

                // Validate configuration first
                var validationErrors = configuration.Validate();
                if (validationErrors.Any())
                {
                    throw new InvalidOperationException($"Invalid Autopilot configuration: {string.Join(", ", validationErrors)}");
                }

                foreach (var mountedImage in mountedImages)
                {
                    var result = ApplyConfigurationToImage(mountedImage, configuration, cmdlet);
                    results.Add(result);
                }

                var successCount = results.Count(r => r.Success);
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Successfully applied Autopilot configuration to {successCount} of {mountedImages.Length} images");

                return results;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    $"Failed to apply Autopilot configuration: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Applies Autopilot configuration to a single mounted image
        /// </summary>
        private AutopilotApplicationResult ApplyConfigurationToImage(
            MountedWindowsImage mountedImage,
            AutopilotConfiguration configuration,
            PSCmdlet? cmdlet = null)
        {
            var result = new AutopilotApplicationResult
            {
                MountedImage = mountedImage,
                Configuration = configuration
            };

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Applying Autopilot configuration to image: {mountedImage.ImageName}");

                // Create the Autopilot directory structure
                var autopilotDir = Path.Combine(mountedImage.MountPath.FullName, "Windows", "Provisioning", "Autopilot");
                if (!Directory.Exists(autopilotDir))
                {
                    Directory.CreateDirectory(autopilotDir);
                }

                // Save the configuration as AutopilotConfigurationFile.json
                var configPath = Path.Combine(autopilotDir, "AutopilotConfigurationFile.json");
                var json = configuration.ToJson(true);
                File.WriteAllText(configPath, json);

                result.Success = true;
                result.AppliedPath = configPath;

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Successfully applied Autopilot configuration to {mountedImage.ImageName} at {configPath}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;

                LoggingService.WriteWarning(cmdlet, ServiceName,
                    $"Failed to apply Autopilot configuration to {mountedImage.ImageName}: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Creates a default Autopilot configuration template
        /// </summary>
        /// <param name="tenantId">Azure AD tenant ID</param>
        /// <param name="tenantDomain">Azure AD tenant domain</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Default Autopilot configuration</returns>
        public AutopilotConfiguration CreateDefaultConfiguration(string tenantId, string tenantDomain, PSCmdlet? cmdlet = null)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName,
                $"Creating default Autopilot configuration for tenant: {tenantDomain}");

            var config = new AutopilotConfiguration
            {
                CloudAssignedTenantId = tenantId,
                CloudAssignedTenantDomain = tenantDomain,
                CloudAssignedDeviceName = "%SERIAL%",
                CloudAssignedAutopilotUpdateTimeout = 1800000,
                CloudAssignedAutopilotUpdateDisabled = 1,
                CloudAssignedForcedEnrollment = 1,
                Version = 2049,
                CommentFile = "Profile Standard Autopilot Deployment Profile",
                CloudAssignedOobeConfig = 286,
                CloudAssignedDomainJoinMethod = 0
            };

            // Generate required IDs and data
            config.GenerateZtdCorrelationId();
            config.GenerateAadServerData("", tenantDomain);

            return config;
        }
    }
}
