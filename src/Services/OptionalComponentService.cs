using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for installing WinPE Optional Components into boot images
    /// </summary>
    public class OptionalComponentService
    {
        private const string ServiceName = "OptionalComponentService";

        /// <summary>
        /// Installs optional components into mounted boot images
        /// </summary>
        /// <param name="mountedImages">Mounted boot images to install components into</param>
        /// <param name="components">Optional components to install</param>
        /// <param name="continueOnError">Whether to continue if individual components fail</param>
        /// <param name="validateInstallation">Whether to validate installation after each component</param>
        /// <param name="cmdlet">Cmdlet for progress reporting and logging</param>
        /// <returns>List of installation results</returns>
        public List<OptionalComponentInstallationResult> InstallOptionalComponents(
            MountedWindowsImage[] mountedImages,
            WinPEOptionalComponent[] components,
            bool continueOnError = false,
            bool validateInstallation = false,
            PSCmdlet? cmdlet = null)
        {
            var results = new List<OptionalComponentInstallationResult>();
            var totalImages = mountedImages.Length;

            LoggingService.WriteVerbose(cmdlet, ServiceName, 
                $"Starting installation of {components.Length} components into {totalImages} mounted images");

            for (int imageIndex = 0; imageIndex < mountedImages.Length; imageIndex++)
            {
                var mountedImage = mountedImages[imageIndex];
                var imageProgress = (int)((double)(imageIndex + 1) / totalImages * 100);

                LoggingService.WriteProgress(cmdlet, "Installing Optional Components",
                    $"[{imageIndex + 1} of {totalImages}] - {mountedImage.ImageName}",
                    $"Processing image {mountedImage.ImageName} ({imageProgress}%)", imageProgress);

                try
                {
                    var result = InstallComponentsIntoSingleImage(
                        mountedImage, 
                        components, 
                        continueOnError, 
                        validateInstallation, 
                        cmdlet);
                    
                    results.Add(result);

                    LoggingService.WriteVerbose(cmdlet, ServiceName, 
                        $"[{imageIndex + 1} of {totalImages}] - Completed {mountedImage.ImageName}: {result.SuccessfulComponents.Count}/{result.TotalComponents} successful");
                }
                catch (Exception ex)
                {
                    LoggingService.WriteError(cmdlet, ServiceName, 
                        $"Failed to install components into {mountedImage.ImageName}", ex);

                    // Create error result
                    var errorResult = new OptionalComponentInstallationResult
                    {
                        MountedImage = mountedImage,
                        FailedComponents = components.ToList(),
                        Errors = new List<string> { ex.Message }
                    };
                    results.Add(errorResult);

                    if (!continueOnError)
                    {
                        throw;
                    }
                }
            }

            // Summary logging
            var totalSuccessful = results.Sum(r => r.SuccessfulComponents.Count);
            var totalFailed = results.Sum(r => r.FailedComponents.Count);
            var totalSkipped = results.Sum(r => r.SkippedComponents.Count);

            LoggingService.WriteVerbose(cmdlet, ServiceName, 
                $"Installation completed: {totalSuccessful} successful, {totalFailed} failed, {totalSkipped} skipped");

            return results;
        }

        /// <summary>
        /// Installs components into a single mounted image
        /// </summary>
        private OptionalComponentInstallationResult InstallComponentsIntoSingleImage(
            MountedWindowsImage mountedImage,
            WinPEOptionalComponent[] components,
            bool continueOnError,
            bool validateInstallation,
            PSCmdlet? cmdlet)
        {
            var startTime = DateTime.UtcNow;
            var result = new OptionalComponentInstallationResult
            {
                MountedImage = mountedImage
            };

            LoggingService.WriteVerbose(cmdlet, ServiceName, 
                $"Installing {components.Length} components into {mountedImage.ImageName} at {mountedImage.MountPath.FullName}");

            // Validate mount path
            if (!mountedImage.MountPath.Exists)
            {
                throw new DirectoryNotFoundException($"Mount path does not exist: {mountedImage.MountPath.FullName}");
            }

            // Sort components by dependencies (basic dependency resolution)
            var sortedComponents = SortComponentsByDependencies(components, cmdlet);
            var totalComponents = sortedComponents.Length;

            for (int i = 0; i < sortedComponents.Length; i++)
            {
                var component = sortedComponents[i];
                var componentProgress = (int)((double)(i + 1) / totalComponents * 100);

                LoggingService.WriteProgress(cmdlet, "Installing Components",
                    $"[{i + 1} of {totalComponents}] - {component.DisplayName}",
                    $"Installing {component.Name} ({componentProgress}%)", componentProgress);

                try
                {
                    // Check if component is already installed
                    if (IsComponentAlreadyInstalled(mountedImage, component, cmdlet))
                    {
                        result.SkippedComponents.Add(component);
                        LoggingService.WriteVerbose(cmdlet, ServiceName, 
                            $"[{i + 1} of {totalComponents}] - Skipped {component.Name} (already installed)");
                        continue;
                    }

                    // Install the component
                    var installSuccess = InstallSingleComponent(mountedImage, component, cmdlet);

                    if (installSuccess)
                    {
                        // Validate installation if requested
                        if (validateInstallation)
                        {
                            var validationSuccess = ValidateComponentInstallation(mountedImage, component, cmdlet);
                            if (!validationSuccess)
                            {
                                throw new InvalidOperationException($"Component {component.Name} installation validation failed");
                            }
                        }

                        result.SuccessfulComponents.Add(component);
                        LoggingService.WriteVerbose(cmdlet, ServiceName, 
                            $"[{i + 1} of {totalComponents}] - Successfully installed {component.Name}");
                    }
                    else
                    {
                        throw new InvalidOperationException($"Component {component.Name} installation failed");
                    }
                }
                catch (Exception ex)
                {
                    result.FailedComponents.Add(component);
                    result.Errors.Add($"{component.Name}: {ex.Message}");
                    
                    LoggingService.WriteWarning(cmdlet, ServiceName, 
                        $"[{i + 1} of {totalComponents}] - Failed to install {component.Name}: {ex.Message}");

                    if (!continueOnError)
                    {
                        throw;
                    }
                }
            }

            result.Duration = DateTime.UtcNow - startTime;

            LoggingService.WriteVerbose(cmdlet, ServiceName, 
                $"Component installation completed for {mountedImage.ImageName}: " +
                $"{result.SuccessfulComponents.Count} successful, {result.FailedComponents.Count} failed, " +
                $"{result.SkippedComponents.Count} skipped in {result.Duration.TotalSeconds:F1} seconds");

            return result;
        }

        /// <summary>
        /// Sorts components by dependencies to ensure proper installation order
        /// </summary>
        private WinPEOptionalComponent[] SortComponentsByDependencies(WinPEOptionalComponent[] components, PSCmdlet? cmdlet)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, "Sorting components by dependencies");

            // Simple dependency resolution - install base components first
            var priorityOrder = new Dictionary<string, int>
            {
                ["WMI"] = 1,
                ["NetFx"] = 2,
                ["PowerShell"] = 3,
                ["Scripting"] = 4,
                ["StorageWMI"] = 5,
                ["RNDIS"] = 6,
                ["SecureStartup"] = 7
            };

            return components
                .OrderBy(c => priorityOrder.TryGetValue(c.Name, out var priority) ? priority : 999)
                .ThenBy(c => c.Name)
                .ToArray();
        }

        /// <summary>
        /// Checks if a component is already installed in the mounted image
        /// </summary>
        private bool IsComponentAlreadyInstalled(MountedWindowsImage mountedImage, WinPEOptionalComponent component, PSCmdlet? cmdlet)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Checking if {component.Name} is already installed");

                // Use DISM to check installed packages
                var dismArgs = $"/Image:\"{mountedImage.MountPath.FullName}\" /Get-Packages /Format:Table";
                var processInfo = new ProcessStartInfo
                {
                    FileName = "dism.exe",
                    Arguments = dismArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, "Failed to start DISM process");
                    return false;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    // Check if component package name appears in the output
                    var packageName = $"WinPE-{component.Name}";
                    var isInstalled = output.IndexOf(packageName, StringComparison.OrdinalIgnoreCase) >= 0;
                    
                    LoggingService.WriteVerbose(cmdlet, ServiceName, 
                        $"Component {component.Name} installation status: {(isInstalled ? "installed" : "not installed")}");
                    
                    return isInstalled;
                }
                else
                {
                    var error = process.StandardError.ReadToEnd();
                    LoggingService.WriteWarning(cmdlet, ServiceName, 
                        $"DISM package check failed: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, 
                    $"Failed to check component installation status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Installs a single component into a mounted image
        /// </summary>
        private bool InstallSingleComponent(MountedWindowsImage mountedImage, WinPEOptionalComponent component, PSCmdlet? cmdlet)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Installing component {component.Name} from {component.ComponentFile.FullName}");

                // Validate component file exists
                if (!component.ComponentFile.Exists)
                {
                    throw new FileNotFoundException($"Component file not found: {component.ComponentFile.FullName}");
                }

                // Use DISM to install the component with process monitoring
                var dismArgs = $"/Image:\"{mountedImage.MountPath.FullName}\" /Add-Package /PackagePath:\"{component.ComponentFile.FullName}\"";

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"DISM install command: dism.exe {dismArgs}");

                var processMonitor = new ProcessMonitoringService();
                var exitCode = processMonitor.ExecuteProcessWithMonitoring(
                    "dism.exe",
                    dismArgs,
                    workingDirectory: null,
                    timeoutMinutes: 15, // Component installation should complete within 15 minutes
                    progressTitle: "Installing WinPE Component",
                    progressDescription: $"Installing {component.Name}",
                    cmdlet);

                if (exitCode == 0)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName,
                        $"Successfully installed component {component.Name}");
                    return true;
                }
                else
                {
                    throw new InvalidOperationException($"DISM installation failed with exit code: {exitCode}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    $"Failed to install component {component.Name}", ex);
                return false;
            }
        }

        /// <summary>
        /// Validates that a component was successfully installed
        /// </summary>
        private bool ValidateComponentInstallation(MountedWindowsImage mountedImage, WinPEOptionalComponent component, PSCmdlet? cmdlet)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Validating installation of component {component.Name}");

                // Use DISM to get package info
                var packageName = $"WinPE-{component.Name}";
                var dismArgs = $"/Image:\"{mountedImage.MountPath.FullName}\" /Get-PackageInfo /PackageName:\"{packageName}\"";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "dism.exe",
                    Arguments = dismArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, "Failed to start DISM validation process");
                    return false;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    // Check if the package is in "Installed" state
                    var isInstalled = output.IndexOf("State : Installed", StringComparison.OrdinalIgnoreCase) >= 0;

                    LoggingService.WriteVerbose(cmdlet, ServiceName,
                        $"Component {component.Name} validation: {(isInstalled ? "passed" : "failed")}");

                    return isInstalled;
                }
                else
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName,
                        $"DISM validation failed for component {component.Name}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName,
                    $"Failed to validate component installation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets installed components from a mounted image
        /// </summary>
        /// <param name="mountedImage">Mounted image to query</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>List of installed component names</returns>
        public List<string> GetInstalledComponents(MountedWindowsImage mountedImage, PSCmdlet? cmdlet = null)
        {
            var installedComponents = new List<string>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Getting installed components from {mountedImage.ImageName}");

                var dismArgs = $"/Image:\"{mountedImage.MountPath.FullName}\" /Get-Packages";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "dism.exe",
                    Arguments = dismArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start DISM process");
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    // Parse output to extract WinPE component names
                    var lines = output.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("Package Identity : WinPE-", StringComparison.OrdinalIgnoreCase))
                        {
                            var packageName = trimmedLine.Substring("Package Identity : ".Length);
                            if (packageName.StartsWith("WinPE-", StringComparison.OrdinalIgnoreCase))
                            {
                                // Extract component name (remove WinPE- prefix and any version suffix)
                                var componentName = packageName.Substring(6); // Remove "WinPE-"
                                var tildaIndex = componentName.IndexOf('~');
                                if (tildaIndex > 0)
                                {
                                    componentName = componentName.Substring(0, tildaIndex);
                                }

                                if (!installedComponents.Contains(componentName, StringComparer.OrdinalIgnoreCase))
                                {
                                    installedComponents.Add(componentName);
                                }
                            }
                        }
                    }

                    LoggingService.WriteVerbose(cmdlet, ServiceName,
                        $"Found {installedComponents.Count} installed WinPE components");
                }
                else
                {
                    var error = process.StandardError.ReadToEnd();
                    LoggingService.WriteWarning(cmdlet, ServiceName,
                        $"Failed to get installed packages: {error}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName,
                    "Failed to get installed components", ex);
            }

            return installedComponents.OrderBy(c => c).ToList();
        }

        /// <summary>
        /// Removes optional components from a mounted image
        /// </summary>
        /// <param name="mountedImage">Mounted image to remove components from</param>
        /// <param name="componentNames">Names of components to remove</param>
        /// <param name="continueOnError">Whether to continue if individual removals fail</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>List of successfully removed component names</returns>
        public List<string> RemoveOptionalComponents(
            MountedWindowsImage mountedImage,
            string[] componentNames,
            bool continueOnError = false,
            PSCmdlet? cmdlet = null)
        {
            var removedComponents = new List<string>();
            var totalComponents = componentNames.Length;

            LoggingService.WriteVerbose(cmdlet, ServiceName,
                $"Removing {totalComponents} components from {mountedImage.ImageName}");

            for (int i = 0; i < componentNames.Length; i++)
            {
                var componentName = componentNames[i];
                var progress = (int)((double)(i + 1) / totalComponents * 100);

                LoggingService.WriteProgress(cmdlet, "Removing Components",
                    $"[{i + 1} of {totalComponents}] - {componentName}",
                    $"Removing {componentName} ({progress}%)", progress);

                try
                {
                    var packageName = $"WinPE-{componentName}";
                    var dismArgs = $"/Image:\"{mountedImage.MountPath.FullName}\" /Remove-Package /PackageName:\"{packageName}\"";

                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "dism.exe",
                        Arguments = dismArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(processInfo);
                    if (process == null)
                    {
                        throw new InvalidOperationException("Failed to start DISM process");
                    }

                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        removedComponents.Add(componentName);
                        LoggingService.WriteVerbose(cmdlet, ServiceName,
                            $"[{i + 1} of {totalComponents}] - Successfully removed {componentName}");
                    }
                    else
                    {
                        var error = process.StandardError.ReadToEnd();
                        throw new InvalidOperationException($"DISM removal failed: {error}");
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName,
                        $"[{i + 1} of {totalComponents}] - Failed to remove {componentName}: {ex.Message}");

                    if (!continueOnError)
                    {
                        throw;
                    }
                }
            }

            LoggingService.WriteVerbose(cmdlet, ServiceName,
                $"Component removal completed: {removedComponents.Count}/{totalComponents} successful");

            return removedComponents;
        }
    }
}
