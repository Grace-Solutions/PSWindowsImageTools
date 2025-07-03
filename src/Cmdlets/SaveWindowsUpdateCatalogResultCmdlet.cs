using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Downloads Windows Update catalog results and returns package objects ready for installation
    /// </summary>
    [Cmdlet(VerbsData.Save, "WindowsUpdateCatalogResult")]
    [OutputType(typeof(WindowsUpdatePackage[]))]
    public class SaveWindowsUpdateCatalogResultCmdlet : PSCmdlet
    {
        /// <summary>
        /// Catalog results to download (from pipeline)
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = "FromPipeline")]
        [ValidateNotNull]
        public WindowsUpdateCatalogResult[] InputObject { get; set; } = null!;

        /// <summary>
        /// Catalog results to download (from parameter)
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "FromParameter")]
        [ValidateNotNull]
        public WindowsUpdateCatalogResult[] CatalogResults { get; set; } = null!;

        /// <summary>
        /// Destination directory for downloads
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNull]
        public DirectoryInfo DestinationPath { get; set; } = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "WindowsUpdates"));

        /// <summary>
        /// Force re-download even if file exists
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Verify file integrity after download
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Verify { get; set; }



        /// <summary>
        /// Enable resume capability for failed downloads
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Resume { get; set; }

        private readonly List<WindowsUpdateCatalogResult> _allCatalogResults = new List<WindowsUpdateCatalogResult>();
        private const string ComponentName = "WindowsUpdateDownload";

        /// <summary>
        /// Processes pipeline input
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                // Collect catalog results from pipeline or parameter
                var resultsToProcess = ParameterSetName == "FromPipeline" ? InputObject : CatalogResults;
                _allCatalogResults.AddRange(resultsToProcess);
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, $"Failed to process record: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Processes all collected catalog results
        /// </summary>
        protected override void EndProcessing()
        {
            try
            {
                if (_allCatalogResults.Count == 0)
                {
                    WriteWarning("No catalog results provided for download");
                    return;
                }

                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName,
                    "Download Windows Updates", $"{_allCatalogResults.Count} catalog results");

                LoggingService.WriteVerbose(this, $"Downloading {_allCatalogResults.Count} catalog results to {DestinationPath.FullName}");

                // Ensure destination directory exists
                if (!DestinationPath.Exists)
                {
                    DestinationPath.Create();
                    LoggingService.WriteVerbose(this, $"Created destination directory: {DestinationPath.FullName}");
                }

                // Filter results that have download URLs
                var downloadableResults = _allCatalogResults.Where(r => r.HasDownloadUrls && r.DownloadUrls.Any()).ToList();
                var nonDownloadableResults = _allCatalogResults.Where(r => !r.HasDownloadUrls || !r.DownloadUrls.Any()).ToList();

                if (nonDownloadableResults.Any())
                {
                    WriteWarning($"{nonDownloadableResults.Count} catalog results do not have download URLs and will be skipped:");
                    foreach (var result in nonDownloadableResults)
                    {
                        WriteWarning($"  {result.KBNumber} - {result.Title}");
                    }
                }

                if (!downloadableResults.Any())
                {
                    WriteWarning("No catalog results have download URLs available");
                    return;
                }

                // Download packages
                var packages = DownloadPackages(downloadableResults);



                // Output results
                foreach (var package in packages)
                {
                    WriteObject(package);
                }

                var successCount = packages.Count(p => p.IsDownloaded);
                var failureCount = packages.Count - successCount;
                var totalCount = downloadableResults.Count;

                var successPercentage = totalCount > 0 ? Math.Round((double)successCount / totalCount * 100, 1) : 0;
                var failurePercentage = totalCount > 0 ? Math.Round((double)failureCount / totalCount * 100, 1) : 0;

                var downloadSummary = FormatUtilityService.FormatCollectionSummary(downloadableResults, "download");
                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Download Windows Updates", operationStartTime,
                    $"Completed {downloadSummary}");

                LoggingService.WriteVerbose(this, $"Succeeded: {successCount} of {totalCount} ({successPercentage}%)");
                LoggingService.WriteVerbose(this, $"Failed: {failureCount} of {totalCount} ({failurePercentage}%)");

                if (failureCount > 0)
                {
                    var failureSummary = FormatUtilityService.FormatCollectionSummary(packages.Where(p => !p.IsDownloaded), "download");
                    WriteWarning($"{failureSummary} failed. Check the ErrorMessage property for details.");
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, $"Windows Update download failed: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Downloads packages from catalog results
        /// </summary>
        private List<WindowsUpdatePackage> DownloadPackages(List<WindowsUpdateCatalogResult> catalogResults)
        {
            var packages = new List<WindowsUpdatePackage>();

            for (int i = 0; i < catalogResults.Count; i++)
            {
                var catalogResult = catalogResults[i];
                var progress = (int)((double)(i + 1) / catalogResults.Count * 100);

                LoggingService.WriteProgress(this, "Downloading Windows Updates",
                    $"[{i + 1} of {catalogResults.Count}] - {catalogResult.KBNumber}",
                    $"Downloading {catalogResult.Title} ({progress}%)", progress);

                try
                {
                    var package = DownloadSinglePackage(catalogResult, i + 1, catalogResults.Count);
                    packages.Add(package);

                    LoggingService.WriteVerbose(this, $"[{i + 1} of {catalogResults.Count}] - {(package.IsDownloaded ? "Successfully downloaded" : "Failed to download")}: {catalogResult.KBNumber}");
                }
                catch (Exception ex)
                {
                    LoggingService.WriteError(this, ComponentName, $"Failed to download {catalogResult.KBNumber}: {ex.Message}", ex);
                    
                    // Create a failed package for tracking
                    var failedPackage = CreatePackageFromCatalogResult(catalogResult);
                    failedPackage.ErrorMessage = ex.Message;
                    packages.Add(failedPackage);
                }
            }

            LoggingService.CompleteProgress(this, "Downloading Windows Updates");
            return packages;
        }

        /// <summary>
        /// Downloads a single package
        /// </summary>
        private WindowsUpdatePackage DownloadSinglePackage(WindowsUpdateCatalogResult catalogResult, int currentIndex, int totalCount)
        {
            var package = CreatePackageFromCatalogResult(catalogResult);

            try
            {
                // Ensure destination directory exists
                if (!DestinationPath.Exists)
                {
                    DestinationPath.Create();
                    LoggingService.WriteVerbose(this, $"Created destination directory: {DestinationPath.FullName}");
                }

                // Generate filename
                var fileName = NetworkService.GetSuggestedFilename(catalogResult.DownloadUrls.First().OriginalString) ?? $"{catalogResult.KBNumber}.cab";
                var filePath = new FileInfo(Path.Combine(DestinationPath.FullName, fileName));

                // Check if file already exists
                if (filePath.Exists && !Force.IsPresent)
                {
                    LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - File already exists: {filePath.FullName}");
                    package.LocalFile = filePath;
                    package.IsDownloaded = true;
                    package.FileSize = filePath.Length;
                    package.DownloadedAt = filePath.LastWriteTime;

                    // Verify if requested
                    if (Verify.IsPresent)
                    {
                        VerifyPackage(package);
                    }

                    return package;
                }

                // Download the package with progress tracking
                LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Downloading {catalogResult.KBNumber} from {catalogResult.DownloadUrls.First().OriginalString}");

                var downloadUrl = catalogResult.DownloadUrls.First();

                // Create progress callback for this specific download
                var progressCallback = ProgressService.CreateDownloadProgressCallback(
                    this,
                    "Downloading Windows Updates",
                    catalogResult.KBNumber,
                    currentIndex,
                    totalCount);

                var downloadResult = DownloadWithResume(downloadUrl, filePath, progressCallback);

                if (downloadResult)
                {
                    // Refresh FileInfo to ensure we have the latest file information
                    filePath.Refresh();

                    // Small delay to ensure file is fully written
                    if (!filePath.Exists)
                    {
                        Thread.Sleep(100);
                        filePath.Refresh();
                    }

                    package.LocalFile = filePath;
                    package.IsDownloaded = true;
                    package.FileSize = filePath.Exists ? filePath.Length : 0;
                    package.DownloadedAt = DateTime.UtcNow;
                    package.DownloadUrl = downloadUrl.OriginalString;

                    // Verify file if requested
                    if (Verify.IsPresent)
                    {
                        VerifyPackage(package);
                    }
                }
                else
                {
                    package.ErrorMessage = "Download failed";
                    LoggingService.WriteWarning(this, $"[{currentIndex} of {totalCount}] - Failed to download {catalogResult.KBNumber}");
                }
            }
            catch (Exception ex)
            {
                package.ErrorMessage = ex.Message;
                LoggingService.WriteError(this, ComponentName, $"[{currentIndex} of {totalCount}] - Failed to download {catalogResult.KBNumber}: {ex.Message}", ex);
            }

            return package;
        }

        /// <summary>
        /// Creates a WindowsUpdatePackage from a WindowsUpdateCatalogResult
        /// </summary>
        private WindowsUpdatePackage CreatePackageFromCatalogResult(WindowsUpdateCatalogResult catalogResult)
        {
            return new WindowsUpdatePackage
            {
                UpdateId = catalogResult.UpdateId,
                KBNumber = catalogResult.KBNumber,
                Title = catalogResult.Title,
                SourceCatalogResult = catalogResult,
                LocalFile = null!, // Will be set during download
                IsDownloaded = false,
                IsVerified = false
            };
        }

        /// <summary>
        /// Verifies a downloaded package
        /// </summary>
        private void VerifyPackage(WindowsUpdatePackage package)
        {
            try
            {
                if (!package.LocalFile.Exists)
                {
                    package.IsVerified = false;
                    package.ErrorMessage = "File does not exist for verification";
                    return;
                }

                // Calculate SHA256 hash
                package.Hash = NetworkService.CalculateFileHash(package.LocalFile.FullName);
                package.IsVerified = true;

                LoggingService.WriteVerbose(this, $"Verified {package.KBNumber}: SHA256 = {package.Hash}");
            }
            catch (Exception ex)
            {
                package.IsVerified = false;
                package.ErrorMessage = $"Verification failed: {ex.Message}";
                LoggingService.WriteWarning(this, $"Failed to verify {package.KBNumber}: {ex.Message}");
            }
        }



        /// <summary>
        /// Formats a byte size into the most appropriate unit (B, KB, MB, GB, TB)
        /// </summary>
        private static string FormatSize(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int unitIndex = 0;
            double size = bytes;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return unitIndex == 0
                ? $"{size:F0} {units[unitIndex]}"
                : $"{size:F2} {units[unitIndex]}";
        }

        /// <summary>
        /// Downloads a file with resume capability and progress tracking
        /// </summary>
        private bool DownloadWithResume(Uri downloadUrl, FileInfo destinationFile, Action<int, string> progressCallback)
        {
            try
            {
                if (string.IsNullOrEmpty(destinationFile?.FullName))
                {
                    throw new ArgumentException("Destination file path is null or empty", nameof(destinationFile));
                }

                // Check if partial file exists and resume is enabled
                long startPosition = 0;
                if (Resume.IsPresent && destinationFile?.Exists == true)
                {
                    startPosition = destinationFile.Length;
                    LoggingService.WriteVerbose(this, $"Resuming download from position: {startPosition:N0} bytes");
                }

                using var httpClient = new HttpClient();
                var handler = new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                    UseDefaultCredentials = true
                };

                using var clientWithHandler = new HttpClient(handler);

                // Create request with range header for resume
                var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                if (startPosition > 0)
                {
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startPosition, null);
                }

                using var response = clientWithHandler.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).Result;

                // Check if server supports range requests
                if (startPosition > 0 && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
                {
                    LoggingService.WriteWarning(this, "Server doesn't support resume, starting fresh download");
                    startPosition = 0;
                    destinationFile?.Delete();
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                }

                var totalBytes = (response.Content.Headers.ContentLength ?? 0) + startPosition;
                var sizeMessage = startPosition > 0
                    ? $"Download size: {FormatSize(totalBytes)} (resuming from {FormatSize(startPosition)})"
                    : $"Download size: {FormatSize(totalBytes)}";
                LoggingService.WriteVerbose(this, sizeMessage);

                using var contentStream = response.Content.ReadAsStreamAsync().Result;
                using var fileStream = new FileStream(destinationFile?.FullName ?? throw new InvalidOperationException("Destination file path is null"),
                    startPosition > 0 ? FileMode.Append : FileMode.Create,
                    FileAccess.Write, FileShare.None, 8192, false);

                var buffer = new byte[8192];
                long totalBytesRead = startPosition;
                int bytesRead;
                var lastProgressReport = 0;

                while ((bytesRead = contentStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progressPercentage = (int)((totalBytesRead * 100) / totalBytes);

                        // Report progress every 1% for better user experience
                        if (progressPercentage > lastProgressReport)
                        {
                            lastProgressReport = progressPercentage;
                            var status = $"Downloaded {FormatSize(totalBytesRead)} of {FormatSize(totalBytes)} ({progressPercentage}%)";
                            progressCallback?.Invoke(progressPercentage, status);
                        }
                    }
                    else
                    {
                        // Unknown size, report bytes downloaded
                        var status = $"Downloaded {FormatSize(totalBytesRead)}";
                        progressCallback?.Invoke(-1, status);
                    }
                }

                LoggingService.WriteVerbose(this, $"Download completed: {FormatSize(totalBytesRead)}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, $"Download failed: {ex.Message}", ex);
                return false;
            }
        }
    }
}
