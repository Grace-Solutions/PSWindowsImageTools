using System;
using System.IO;
using System.Net.Http;
using System.Management.Automation;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for network operations including file downloads with progress reporting
    /// </summary>
    public static class NetworkService
    {
        private const string ServiceName = "NetworkService";

        /// <summary>
        /// Downloads a file from URL with progress reporting
        /// </summary>
        /// <param name="url">URL to download from</param>
        /// <param name="destinationPath">Local path to save the file</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <param name="progressCallback">Progress callback for reporting download progress</param>
        /// <returns>True if download succeeded</returns>
        public static bool DownloadFile(string url, string destinationPath, PSCmdlet? cmdlet = null, Action<int, string>? progressCallback = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Starting download from: {url}");
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Destination: {destinationPath}");

                // Create destination directory if it doesn't exist
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Created directory: {destinationDir}");
                }

                using var httpClient = new HttpClient();
                
                // Configure to ignore certificate errors and use default credentials
                var handler = new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                    UseDefaultCredentials = true
                };
                
                using var clientWithHandler = new HttpClient(handler);

                // Get the response
                using var response = clientWithHandler.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result;
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Download size: {totalBytes:N0} bytes");

                using var contentStream = response.Content.ReadAsStreamAsync().Result;
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, false);

                var buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;
                var lastProgressReport = 0;

                while ((bytesRead = contentStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progressPercentage = (int)((totalBytesRead * 100) / totalBytes);
                        
                        // Report progress every 5% to avoid too many updates
                        if (progressPercentage >= lastProgressReport + 5)
                        {
                            lastProgressReport = progressPercentage;
                            var status = $"Downloaded {totalBytesRead:N0} of {totalBytes:N0} bytes ({progressPercentage}%)";
                            progressCallback?.Invoke(progressPercentage, status);
                        }
                    }
                    else
                    {
                        // Unknown size, report bytes downloaded
                        var status = $"Downloaded {totalBytesRead:N0} bytes";
                        progressCallback?.Invoke(-1, status);
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Download completed: {totalBytesRead:N0} bytes");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Download failed: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Copies a file from UNC path with progress reporting
        /// </summary>
        /// <param name="sourcePath">UNC source path</param>
        /// <param name="destinationPath">Local destination path</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <param name="progressCallback">Progress callback for reporting copy progress</param>
        /// <returns>True if copy succeeded</returns>
        public static bool CopyFromUNC(string sourcePath, string destinationPath, PSCmdlet? cmdlet = null, Action<int, string>? progressCallback = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Starting UNC copy from: {sourcePath}");
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Destination: {destinationPath}");

                if (!File.Exists(sourcePath))
                {
                    LoggingService.WriteError(cmdlet, ServiceName, $"Source file not found: {sourcePath}");
                    return false;
                }

                // Create destination directory if it doesn't exist
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Created directory: {destinationDir}");
                }

                var sourceInfo = new FileInfo(sourcePath);
                var totalBytes = sourceInfo.Length;
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Copy size: {totalBytes:N0} bytes");

                using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192);
                using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192);

                var buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;
                var lastProgressReport = 0;

                while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    destinationStream.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    var progressPercentage = (int)((totalBytesRead * 100) / totalBytes);
                    
                    // Report progress every 5% to avoid too many updates
                    if (progressPercentage >= lastProgressReport + 5)
                    {
                        lastProgressReport = progressPercentage;
                        var status = $"Copied {totalBytesRead:N0} of {totalBytes:N0} bytes ({progressPercentage}%)";
                        progressCallback?.Invoke(progressPercentage, status);
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"UNC copy completed: {totalBytesRead:N0} bytes");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"UNC copy failed: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets the filename from a URL or UNC path, using Content-Disposition header if available
        /// </summary>
        /// <param name="urlOrPath">URL or UNC path</param>
        /// <param name="cmdlet">Cmdlet for logging</param>
        /// <returns>Suggested filename</returns>
        public static string GetSuggestedFilename(string urlOrPath, PSCmdlet? cmdlet = null)
        {
            try
            {
                if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    // For URLs, try to get filename from Content-Disposition header
                    using var httpClient = new HttpClient();
                    var handler = new HttpClientHandler()
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                        UseDefaultCredentials = true
                    };
                    
                    using var clientWithHandler = new HttpClient(handler);
                    using var response = clientWithHandler.SendAsync(new HttpRequestMessage(HttpMethod.Head, urlOrPath)).Result;
                    
                    if (response.Content.Headers.ContentDisposition?.FileName != null)
                    {
                        var filename = response.Content.Headers.ContentDisposition.FileName.Trim('"');
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"Using filename from Content-Disposition: {filename}");
                        return filename;
                    }
                }
                
                // Fall back to extracting from URL/path
                var pathFilename = Path.GetFileName(urlOrPath);
                if (!string.IsNullOrEmpty(pathFilename))
                {
                    return pathFilename;
                }
                
                // Last resort: generate a filename
                return $"download_{DateTime.UtcNow:yyyyMMdd_HHmmss}.tmp";
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(cmdlet, ServiceName, $"Could not determine filename from {urlOrPath}: {ex.Message}");
                return Path.GetFileName(urlOrPath) ?? $"download_{DateTime.UtcNow:yyyyMMdd_HHmmss}.tmp";
            }
        }

        /// <summary>
        /// Calculates SHA256 hash of a file
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>SHA256 hash as hex string</returns>
        public static string CalculateFileHash(string filePath)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
