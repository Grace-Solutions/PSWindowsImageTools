using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Text.RegularExpressions;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for parsing Microsoft's ADK download page and retrieving download links
    /// </summary>
    public class ADKDownloadService
    {
        private const string ServiceName = "ADKDownloadService";
        private const string ADKDownloadPageUrl = "https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install";
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Information about available ADK downloads
        /// </summary>
        public class ADKDownloadInfo
        {
            public string Version { get; set; } = string.Empty;
            public string ReleaseDate { get; set; } = string.Empty;
            public string ADKDownloadUrl { get; set; } = string.Empty;
            public string WinPEDownloadUrl { get; set; } = string.Empty;
            public string? PatchDownloadUrl { get; set; }
            public bool HasPatch { get; set; }
            public List<string> SupportedOSVersions { get; set; } = new List<string>();
            public string Description { get; set; } = string.Empty;
        }

        /// <summary>
        /// Parses the Microsoft ADK download page to get the latest download information
        /// </summary>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>ADK download information</returns>
        public ADKDownloadInfo? GetLatestADKDownloadInfo(PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Fetching ADK download page: {ADKDownloadPageUrl}");

                // Download the page content
                var response = _httpClient.GetStringAsync(ADKDownloadPageUrl).Result;
                
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Downloaded page content ({response.Length} characters)");

                // Parse the content
                var downloadInfo = ParseADKDownloadPage(response, cmdlet);
                
                if (downloadInfo != null)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, 
                        $"Found ADK version {downloadInfo.Version} ({downloadInfo.ReleaseDate})");
                }
                else
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, "Failed to parse ADK download information");
                }

                return downloadInfo;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, "Failed to get ADK download information", ex);
                return null;
            }
        }

        /// <summary>
        /// Parses the HTML content of the ADK download page
        /// </summary>
        private ADKDownloadInfo? ParseADKDownloadPage(string htmlContent, PSCmdlet? cmdlet)
        {
            try
            {
                var downloadInfo = new ADKDownloadInfo();

                // Parse version and release date - find the FIRST occurrence (latest)
                // Pattern: "Download the Windows ADK [version] ([Month] [Year])"
                var versionPattern = @"Download\s+the\s+(?:Windows\s+)?ADK\s+([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)\s*\(\s*([A-Za-z]+\s+[0-9]{4})\s*\)";
                var versionMatch = Regex.Match(htmlContent, versionPattern, RegexOptions.IgnoreCase);

                if (versionMatch.Success)
                {
                    downloadInfo.Version = versionMatch.Groups[1].Value.Trim();
                    downloadInfo.ReleaseDate = versionMatch.Groups[2].Value.Trim();

                    LoggingService.WriteVerbose(cmdlet, ServiceName,
                        $"Parsed latest ADK version: {downloadInfo.Version}, release: {downloadInfo.ReleaseDate}");
                }
                else
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, "Could not parse ADK version information");
                }

                // Parse supported OS versions
                // Look for patterns like "Windows 11, version 24H2 and all earlier supported versions"
                var osPattern = @"Windows\s+(?:11|10|Server)[^.]*(?:and all earlier supported versions|Windows Server [0-9]+)";
                var osMatches = Regex.Matches(htmlContent, osPattern, RegexOptions.IgnoreCase);
                
                foreach (Match match in osMatches)
                {
                    var osVersion = match.Value.Trim();
                    if (!downloadInfo.SupportedOSVersions.Contains(osVersion))
                    {
                        downloadInfo.SupportedOSVersions.Add(osVersion);
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Found {downloadInfo.SupportedOSVersions.Count} supported OS versions");

                // Parse download links for the specific version found
                var downloadLinks = ParseDownloadLinks(htmlContent, downloadInfo.Version, downloadInfo.ReleaseDate, cmdlet);

                if (downloadLinks.ContainsKey("ADK"))
                {
                    downloadInfo.ADKDownloadUrl = downloadLinks["ADK"];
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"ADK download URL: {downloadInfo.ADKDownloadUrl}");
                }

                if (downloadLinks.ContainsKey("WinPE"))
                {
                    downloadInfo.WinPEDownloadUrl = downloadLinks["WinPE"];
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"WinPE download URL: {downloadInfo.WinPEDownloadUrl}");
                }

                if (downloadLinks.ContainsKey("Patch"))
                {
                    downloadInfo.PatchDownloadUrl = downloadLinks["Patch"];
                    downloadInfo.HasPatch = true;
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"ADK patch URL: {downloadInfo.PatchDownloadUrl}");
                }

                // Create description
                downloadInfo.Description = $"Windows ADK {downloadInfo.Version} ({downloadInfo.ReleaseDate})";

                // Validate that we have essential information
                if (string.IsNullOrEmpty(downloadInfo.Version) || string.IsNullOrEmpty(downloadInfo.ADKDownloadUrl))
                {
                    LoggingService.WriteWarning(cmdlet, ServiceName, "Missing essential ADK information");
                    return null;
                }

                return downloadInfo;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, "Failed to parse ADK download page", ex);
                return null;
            }
        }

        /// <summary>
        /// Parses download links from the HTML content for a specific version
        /// </summary>
        private Dictionary<string, string> ParseDownloadLinks(string htmlContent, string version, string releaseDate, PSCmdlet? cmdlet)
        {
            var links = new Dictionary<string, string>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Looking for download links for ADK {version} ({releaseDate})");

                // Find the section containing the specific version and release date
                var versionSectionPattern = $@"Download\s+the\s+(?:Windows\s+)?ADK\s+{Regex.Escape(version)}\s*\(\s*{Regex.Escape(releaseDate)}\s*\)(.*?)(?=Download\s+the\s+(?:Windows\s+)?ADK\s+[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+|$)";
                var versionSectionMatch = Regex.Match(htmlContent, versionSectionPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                string searchContent = versionSectionMatch.Success ? versionSectionMatch.Groups[1].Value : htmlContent;

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Searching in content section ({searchContent.Length} characters)");

                // Look for ADK download link - first occurrence after version declaration
                var adkPattern = @"Download\s+the\s+(?:Windows\s+)?ADK\s+" + Regex.Escape(version) + @"[^<]*?href\s*=\s*[""']([^""']+)[""']";
                var adkMatch = Regex.Match(htmlContent, adkPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (adkMatch.Success)
                {
                    links["ADK"] = adkMatch.Groups[1].Value;
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found ADK link: {links["ADK"]}");
                }

                // Look for WinPE add-on download link
                var winpePattern = @"Download\s+the\s+(?:Windows\s+)?PE\s+add-on\s+for\s+the\s+(?:Windows\s+)?ADK\s+" + Regex.Escape(version) + @"[^<]*?href\s*=\s*[""']([^""']+)[""']";
                var winpeMatch = Regex.Match(htmlContent, winpePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (winpeMatch.Success)
                {
                    links["WinPE"] = winpeMatch.Groups[1].Value;
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found WinPE link: {links["WinPE"]}");
                }

                // Look for patch download link
                var patchPattern = @"(?:latest\s+)?(?:ADK\s+)?patch\s+for\s+ADK\s+" + Regex.Escape(version) + @"[^<]*?href\s*=\s*[""']([^""']+)[""']";
                var patchMatch = Regex.Match(htmlContent, patchPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (patchMatch.Success)
                {
                    links["Patch"] = patchMatch.Groups[1].Value;
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found patch link: {links["Patch"]}");
                }

                // Fallback: Look for generic download links if specific patterns fail
                if (links.Count == 0)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Specific patterns failed, trying fallback patterns");

                    var fallbackPatterns = new[]
                    {
                        @"href\s*=\s*[""']([^""']*(?:adksetup|winpeaddons)[^""']*\.exe)[""']",
                        @"href\s*=\s*[""'](https://(?:www\.)?microsoft\.com/[^""']*download[^""']*)[""']",
                        @"href\s*=\s*[""'](https://go\.microsoft\.com/fwlink/?\?[^""']*)[""']"
                    };

                    foreach (var pattern in fallbackPatterns)
                    {
                        var matches = Regex.Matches(searchContent, pattern, RegexOptions.IgnoreCase);

                        foreach (Match match in matches)
                        {
                            var url = match.Groups[1].Value;

                            if (url.IndexOf("adksetup", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                url.IndexOf("winpe", StringComparison.OrdinalIgnoreCase) < 0 &&
                                !links.ContainsKey("ADK"))
                            {
                                links["ADK"] = url;
                                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found fallback ADK link: {url}");
                            }
                            else if ((url.IndexOf("winpe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     url.IndexOf("adkwinpeaddons", StringComparison.OrdinalIgnoreCase) >= 0) &&
                                     !links.ContainsKey("WinPE"))
                            {
                                links["WinPE"] = url;
                                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found fallback WinPE link: {url}");
                            }
                            else if (url.IndexOf(".zip", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                     !links.ContainsKey("Patch"))
                            {
                                links["Patch"] = url;
                                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found fallback patch link: {url}");
                            }
                        }
                    }
                }

                // If we didn't find direct links, look for Microsoft Download Center patterns
                if (links.Count == 0)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "No direct download links found, looking for download center links");
                    
                    // Look for text patterns that might indicate download sections
                    var downloadSectionPattern = @"(?i)(?:download|get)\s+(?:the\s+)?(?:windows\s+)?adk[^.]*?(?:href=[""']([^""']+)[""']|https://[^\s<>""']+)";
                    var sectionMatches = Regex.Matches(htmlContent, downloadSectionPattern);
                    
                    foreach (Match match in sectionMatches)
                    {
                        if (match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                        {
                            var url = match.Groups[1].Value;
                            if (!links.ContainsKey("ADK"))
                            {
                                links["ADK"] = url;
                                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found potential ADK link: {url}");
                            }
                        }
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Parsed {links.Count} download links");
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to parse download links: {ex.Message}");
            }

            return links;
        }

        /// <summary>
        /// Downloads a file from the specified URL
        /// </summary>
        /// <param name="url">URL to download from</param>
        /// <param name="destinationPath">Local file path to save to</param>
        /// <param name="cmdlet">Cmdlet for progress reporting</param>
        /// <returns>True if download was successful</returns>
        public bool DownloadFile(string url, string destinationPath, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Downloading from: {url}");
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Saving to: {destinationPath}");

                // Ensure destination directory exists
                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var response = _httpClient.GetAsync(url).Result;
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Download size: {FormatBytes(totalBytes)}");

                using var contentStream = response.Content.ReadAsStreamAsync().Result;
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);

                var buffer = new byte[8192];
                var totalBytesRead = 0L;
                int bytesRead;

                while ((bytesRead = contentStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = (int)((double)totalBytesRead / totalBytes * 100);
                        LoggingService.WriteProgress(cmdlet, "Downloading ADK",
                            $"Downloaded {FormatBytes(totalBytesRead)} of {FormatBytes(totalBytes)}",
                            $"Progress: {progress}%", progress);
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Download completed: {FormatBytes(totalBytesRead)}");

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, "Failed to download file", ex);
                return false;
            }
        }

        /// <summary>
        /// Formats byte count into human-readable string
        /// </summary>
        private string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB)
                return $"{bytes / (double)GB:F2} GB";
            if (bytes >= MB)
                return $"{bytes / (double)MB:F2} MB";
            if (bytes >= KB)
                return $"{bytes / (double)KB:F2} KB";
            
            return $"{bytes} bytes";
        }

        /// <summary>
        /// Downloads and extracts ADK patch, then installs MSP files
        /// </summary>
        /// <param name="patchUrl">URL of the patch ZIP file</param>
        /// <param name="cmdlet">Cmdlet for progress reporting</param>
        /// <returns>True if patch was successfully applied</returns>
        public bool DownloadAndInstallPatch(string patchUrl, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Starting ADK patch installation from: {patchUrl}");

                var tempDir = Path.Combine(Path.GetTempPath(), $"ADKPatch_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    // Download patch ZIP file
                    var patchZipPath = Path.Combine(tempDir, "adk_patch.zip");
                    LoggingService.WriteProgress(cmdlet, "Installing ADK Patch", "Downloading patch", "Downloading patch file", 10);

                    var downloadSuccess = DownloadFile(patchUrl, patchZipPath, cmdlet);
                    if (!downloadSuccess || !File.Exists(patchZipPath))
                    {
                        throw new InvalidOperationException("Failed to download ADK patch");
                    }

                    // Extract patch ZIP file
                    LoggingService.WriteProgress(cmdlet, "Installing ADK Patch", "Extracting patch", "Extracting patch files", 30);

                    var extractDir = Path.Combine(tempDir, "extracted");
                    ExtractZipFile(patchZipPath, extractDir, cmdlet);

                    // Find and install MSP files
                    LoggingService.WriteProgress(cmdlet, "Installing ADK Patch", "Installing patches", "Applying MSP files", 50);

                    var mspFiles = Directory.GetFiles(extractDir, "*.msp", SearchOption.AllDirectories);
                    if (mspFiles.Length == 0)
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName, "No MSP files found in patch archive");
                        return false;
                    }

                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found {mspFiles.Length} MSP files to install");

                    var processMonitor = new ProcessMonitoringService();
                    var successCount = 0;

                    for (int i = 0; i < mspFiles.Length; i++)
                    {
                        var mspFile = mspFiles[i];
                        var fileName = Path.GetFileName(mspFile);
                        var progress = 50 + (int)((double)(i + 1) / mspFiles.Length * 40);

                        LoggingService.WriteProgress(cmdlet, "Installing ADK Patch",
                            $"Installing patch {i + 1} of {mspFiles.Length}",
                            $"Applying {fileName} ({progress}%)", progress);

                        try
                        {
                            LoggingService.WriteVerbose(cmdlet, ServiceName, $"Installing MSP: {fileName}");

                            // Use msiexec to install the MSP file silently
                            var msiexecArgs = $"/update \"{mspFile}\" /quiet /norestart";

                            var exitCode = processMonitor.ExecuteProcessWithMonitoring(
                                "msiexec.exe",
                                msiexecArgs,
                                workingDirectory: null,
                                timeoutMinutes: 15, // MSP installation should be quick
                                progressTitle: "Installing ADK Patch",
                                progressDescription: $"Applying {fileName}",
                                cmdlet);

                            if (exitCode == 0)
                            {
                                successCount++;
                                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Successfully installed {fileName}");
                            }
                            else
                            {
                                LoggingService.WriteWarning(cmdlet, ServiceName,
                                    $"Failed to install {fileName} (exit code: {exitCode})");
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggingService.WriteWarning(cmdlet, ServiceName,
                                $"Error installing {fileName}: {ex.Message}");
                        }
                    }

                    LoggingService.WriteProgress(cmdlet, "Installing ADK Patch", "Patch installation complete",
                        $"Installed {successCount} of {mspFiles.Length} patches", 100);

                    LoggingService.WriteVerbose(cmdlet, ServiceName,
                        $"Patch installation completed: {successCount}/{mspFiles.Length} successful");

                    return successCount > 0; // Success if at least one patch was installed
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
                LoggingService.WriteError(cmdlet, ServiceName, "Failed to install ADK patch", ex);
                return false;
            }
        }

        /// <summary>
        /// Extracts a ZIP file to the specified directory
        /// </summary>
        private void ExtractZipFile(string zipFilePath, string extractToDirectory, PSCmdlet? cmdlet)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Extracting {zipFilePath} to {extractToDirectory}");

                if (!Directory.Exists(extractToDirectory))
                {
                    Directory.CreateDirectory(extractToDirectory);
                }

                // Use ZipFile for extraction
                ZipFile.ExtractToDirectory(zipFilePath, extractToDirectory);

                LoggingService.WriteVerbose(cmdlet, ServiceName, "ZIP extraction completed successfully");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, "Failed to extract ZIP file", ex);
                throw;
            }
        }
    }
}
