using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for downloading, installing, and uninstalling Windows ADK
    /// </summary>
    public class ADKManagementService
    {
        private const string ServiceName = "ADKManagementService";
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Downloads and installs the latest Windows ADK silently
        /// </summary>
        /// <param name="installPath">Custom installation path (optional)</param>
        /// <param name="includeWinPE">Whether to include WinPE add-on</param>
        /// <param name="includeDeploymentTools">Whether to include Deployment Tools</param>
        /// <param name="cmdlet">Cmdlet for progress reporting</param>
        /// <returns>Information about the installed ADK</returns>
        public ADKInfo? InstallLatestADK(
            string? installPath = null,
            bool includeWinPE = true,
            bool includeDeploymentTools = true,
            PSCmdlet? cmdlet = null)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, "Starting ADK installation process");

            try
            {
                // Check if ADK is already installed
                var adkService = new ADKService();
                var existingInstallations = adkService.DetectADKInstallations(cmdlet);
                
                if (existingInstallations.Any())
                {
                    var latest = existingInstallations.OrderByDescending(adk => adk.Version).First();
                    LoggingService.WriteVerbose(cmdlet, ServiceName, 
                        $"ADK already installed: {latest.DisplayName} v{latest.Version}");
                    
                    // Check if it has required components
                    if ((!includeWinPE || latest.HasWinPEAddon) && 
                        (!includeDeploymentTools || latest.HasDeploymentTools))
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName, "Existing ADK meets requirements");
                        return latest;
                    }
                }

                // Download ADK installer
                LoggingService.WriteProgress(cmdlet, "Installing Windows ADK", "Getting download information", "Fetching latest ADK info", 10);

                var downloadService = new ADKDownloadService();
                var downloadInfo = downloadService.GetLatestADKDownloadInfo(cmdlet);

                if (downloadInfo == null || string.IsNullOrEmpty(downloadInfo.ADKDownloadUrl))
                {
                    throw new InvalidOperationException("Failed to get ADK download information");
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Found ADK {downloadInfo.Version} ({downloadInfo.ReleaseDate})");

                var tempDir = Path.Combine(Path.GetTempPath(), $"ADKInstall_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    LoggingService.WriteProgress(cmdlet, "Installing Windows ADK", "Downloading ADK installer", "Downloading installer", 20);

                    var adkInstallerPath = Path.Combine(tempDir, "adksetup.exe");
                    var downloadSuccess = downloadService.DownloadFile(downloadInfo.ADKDownloadUrl, adkInstallerPath, cmdlet);

                    if (!downloadSuccess || !File.Exists(adkInstallerPath))
                    {
                        throw new InvalidOperationException("Failed to download ADK installer");
                    }

                    // Download WinPE add-on if requested and available
                    string? winpeInstallerPath = null;
                    if (includeWinPE && !string.IsNullOrEmpty(downloadInfo.WinPEDownloadUrl))
                    {
                        LoggingService.WriteProgress(cmdlet, "Installing Windows ADK", "Downloading WinPE add-on", "Downloading WinPE installer", 40);

                        winpeInstallerPath = Path.Combine(tempDir, "adkwinpesetup.exe");
                        var winpeDownloadSuccess = downloadService.DownloadFile(downloadInfo.WinPEDownloadUrl, winpeInstallerPath, cmdlet);

                        if (!winpeDownloadSuccess)
                        {
                            LoggingService.WriteWarning(cmdlet, ServiceName, "Failed to download WinPE add-on, will install without it");
                            winpeInstallerPath = null;
                        }
                    }

                    // Install ADK
                    LoggingService.WriteProgress(cmdlet, "Installing Windows ADK", "Installing ADK", "Running installer", 60);

                    var installedADK = InstallADKSilently(adkInstallerPath, winpeInstallerPath, installPath, includeWinPE, includeDeploymentTools, cmdlet);

                    // Install patch if available
                    if (installedADK != null && downloadInfo.HasPatch && !string.IsNullOrEmpty(downloadInfo.PatchDownloadUrl))
                    {
                        LoggingService.WriteProgress(cmdlet, "Installing Windows ADK", "Installing patch", "Applying ADK patch", 85);

                        try
                        {
                            var patchSuccess = downloadService.DownloadAndInstallPatch(downloadInfo.PatchDownloadUrl!, cmdlet);
                            if (patchSuccess)
                            {
                                LoggingService.WriteVerbose(cmdlet, ServiceName, "ADK patch installed successfully");
                            }
                            else
                            {
                                LoggingService.WriteWarning(cmdlet, ServiceName, "ADK patch installation failed or was incomplete");
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggingService.WriteWarning(cmdlet, ServiceName, $"ADK patch installation failed: {ex.Message}");
                        }
                    }

                    LoggingService.WriteProgress(cmdlet, "Installing Windows ADK", "Installation complete", "Verifying installation", 100);

                    return installedADK;
                }
                finally
                {
                    // Cleanup temp directory
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName, 
                            $"Failed to cleanup temp directory: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, "Failed to install ADK", ex);
                throw;
            }
        }

        /// <summary>
        /// Uninstalls Windows ADK and WinPE add-on silently
        /// </summary>
        /// <param name="removeAll">Whether to remove all ADK installations</param>
        /// <param name="cmdlet">Cmdlet for progress reporting</param>
        /// <returns>True if uninstallation was successful</returns>
        public bool UninstallADK(bool removeAll = false, PSCmdlet? cmdlet = null)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, "Starting ADK uninstallation process");

            try
            {
                var adkService = new ADKService();
                var installations = adkService.DetectADKInstallations(cmdlet);

                if (!installations.Any())
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "No ADK installations found to uninstall");
                    return true;
                }

                var installationsToRemove = removeAll ? installations.ToArray() : new[] { installations.OrderByDescending(adk => adk.Version).First() };
                var totalInstallations = installationsToRemove.Count();
                var successCount = 0;

                for (int i = 0; i < installationsToRemove.Count(); i++)
                {
                    var installation = installationsToRemove.ElementAt(i);
                    var progress = (int)((double)(i + 1) / totalInstallations * 100);

                    LoggingService.WriteProgress(cmdlet, "Uninstalling Windows ADK",
                        $"[{i + 1} of {totalInstallations}] - {installation.DisplayName}",
                        $"Uninstalling {installation.DisplayName} ({progress}%)", progress);

                    try
                    {
                        if (UninstallSingleADK(installation, cmdlet))
                        {
                            successCount++;
                            LoggingService.WriteVerbose(cmdlet, ServiceName, 
                                $"Successfully uninstalled {installation.DisplayName}");
                        }
                        else
                        {
                            LoggingService.WriteWarning(cmdlet, ServiceName, 
                                $"Failed to uninstall {installation.DisplayName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteError(cmdlet, ServiceName, 
                            $"Error uninstalling {installation.DisplayName}", ex);
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Uninstallation completed: {successCount}/{totalInstallations} successful");

                return successCount == totalInstallations;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, "Failed to uninstall ADK", ex);
                return false;
            }
        }



        /// <summary>
        /// Installs ADK silently using the downloaded installers
        /// </summary>
        private ADKInfo? InstallADKSilently(
            string adkInstallerPath,
            string? winpeInstallerPath,
            string? installPath,
            bool includeWinPE,
            bool includeDeploymentTools,
            PSCmdlet? cmdlet)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, "Starting silent ADK installation");

                // Build installation arguments
                var args = new List<string>
                {
                    "/quiet",
                    "/norestart"
                };

                if (!string.IsNullOrEmpty(installPath))
                {
                    args.Add($"/installpath \"{installPath}\"");
                }

                // Specify features to install
                var features = new List<string>();
                if (includeDeploymentTools)
                {
                    features.Add("OptionId.DeploymentTools");
                }
                if (includeWinPE)
                {
                    features.Add("OptionId.WindowsPreinstallationEnvironment");
                }

                if (features.Any())
                {
                    args.Add($"/features {string.Join(" ", features)}");
                }

                var arguments = string.Join(" ", args);

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"ADK installation command: {adkInstallerPath} {arguments}");

                // Install main ADK first
                var processMonitor = new ProcessMonitoringService();
                var exitCode = processMonitor.ExecuteProcessWithMonitoring(
                    adkInstallerPath,
                    arguments,
                    workingDirectory: null,
                    timeoutMinutes: 60, // ADK installation can take up to 60 minutes
                    progressTitle: "Installing Windows ADK",
                    progressDescription: "Running ADK installer",
                    cmdlet);

                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"ADK installation failed with exit code {exitCode}");
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, "ADK installation completed successfully");

                // Install WinPE add-on if available and requested
                if (includeWinPE && !string.IsNullOrEmpty(winpeInstallerPath) && File.Exists(winpeInstallerPath))
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Installing WinPE add-on");

                    var winpeArgs = "/quiet /norestart";
                    LoggingService.WriteVerbose(cmdlet, ServiceName,
                        $"WinPE installation command: {winpeInstallerPath} {winpeArgs}");

                    var winpeExitCode = processMonitor.ExecuteProcessWithMonitoring(
                        winpeInstallerPath!,
                        winpeArgs,
                        workingDirectory: null,
                        timeoutMinutes: 30, // WinPE add-on should be faster
                        progressTitle: "Installing WinPE Add-on",
                        progressDescription: "Running WinPE installer",
                        cmdlet);

                    if (winpeExitCode == 0)
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName, "WinPE add-on installation completed successfully");
                    }
                    else
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName, $"WinPE add-on installation failed with exit code {winpeExitCode}");
                    }
                }

                // Verify installation
                System.Threading.Thread.Sleep(5000); // Give time for installation to complete and registry to update
                var adkService = new ADKService();
                var installations = adkService.DetectADKInstallations(cmdlet);
                return installations.OrderByDescending(adk => adk.Version).FirstOrDefault();
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, "Failed to install ADK silently", ex);
                return null;
            }
        }

        /// <summary>
        /// Uninstalls a single ADK installation
        /// </summary>
        private bool UninstallSingleADK(ADKInfo installation, PSCmdlet? cmdlet)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Uninstalling {installation.DisplayName} from {installation.InstallationPath?.FullName}");

                // Try to find uninstaller
                var uninstallerPaths = new[]
                {
                    Path.Combine(installation.InstallationPath?.FullName ?? "", "Installers", "Windows Assessment and Deployment Kit", "adksetup.exe"),
                    Path.Combine(installation.InstallationPath?.FullName ?? "", "adksetup.exe")
                };

                var uninstallerPath = uninstallerPaths.FirstOrDefault(File.Exists);
                
                if (!string.IsNullOrEmpty(uninstallerPath))
                {
                    // Use ADK's own uninstaller with process monitoring
                    var arguments = "/uninstall /quiet /norestart";

                    LoggingService.WriteVerbose(cmdlet, ServiceName,
                        $"ADK uninstall command: {uninstallerPath} {arguments}");

                    var processMonitor = new ProcessMonitoringService();
                    var exitCode = processMonitor.ExecuteProcessWithMonitoring(
                        uninstallerPath,
                        arguments,
                        workingDirectory: null,
                        timeoutMinutes: 30, // Uninstallation should be faster than installation
                        progressTitle: "Uninstalling Windows ADK",
                        progressDescription: $"Removing {installation.DisplayName}",
                        cmdlet);

                    return exitCode == 0;
                }

                // Fallback: Use Windows uninstaller via registry
                return UninstallViaRegistry(installation, cmdlet);
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, 
                    $"Failed to uninstall {installation.DisplayName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Uninstalls ADK using Windows uninstaller from registry
        /// </summary>
        private bool UninstallViaRegistry(ADKInfo installation, PSCmdlet? cmdlet)
        {
            try
            {
                // Extract uninstall string from registry key path
                var keyPath = installation.RegistryKey;
                if (string.IsNullOrEmpty(keyPath))
                {
                    return false;
                }

                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath.Replace("HKEY_LOCAL_MACHINE\\", ""));
                if (key == null)
                {
                    return false;
                }

                var uninstallString = key.GetValue("UninstallString")?.ToString();
                if (string.IsNullOrEmpty(uninstallString))
                {
                    return false;
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Using uninstall string: {uninstallString}");

                // Parse and execute uninstall command
                var parts = uninstallString!.Split(new char[] { ' ' }, 2);
                var executable = parts[0].Trim('"');
                var arguments = parts.Length > 1 ? parts[1] + " /quiet /norestart" : "/quiet /norestart";

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Registry uninstall command: {executable} {arguments}");

                var processMonitor = new ProcessMonitoringService();
                var exitCode = processMonitor.ExecuteProcessWithMonitoring(
                    executable,
                    arguments,
                    workingDirectory: null,
                    timeoutMinutes: 30,
                    progressTitle: "Uninstalling Windows ADK",
                    progressDescription: $"Removing {installation.DisplayName} via registry",
                    cmdlet);

                return exitCode == 0;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, 
                    "Failed to uninstall via registry", ex);
                return false;
            }
        }
    }
}
