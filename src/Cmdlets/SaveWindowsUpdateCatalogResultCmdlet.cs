using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
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
        /// Skip database update after download
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter SkipDatabase { get; set; }

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

                // Update database if not skipped
                if (!SkipDatabase.IsPresent && !ConfigurationService.IsDatabaseDisabled)
                {
                    UpdateDatabase(packages);
                }

                // Output results
                foreach (var package in packages)
                {
                    WriteObject(package);
                }

                var successCount = packages.Count(p => p.IsDownloaded);
                var failureCount = packages.Count - successCount;

                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Download Windows Updates", operationStartTime,
                    $"Downloaded {successCount} of {downloadableResults.Count} packages. {failureCount} failed.");

                if (failureCount > 0)
                {
                    WriteWarning($"{failureCount} downloads failed. Check the ErrorMessage property for details.");
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
                // Generate filename
                var fileName = NetworkService.GetSuggestedFilename(catalogResult.DownloadUrls.First()) ?? $"{catalogResult.KBNumber}.cab";
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

                // Download the package
                LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Downloading {catalogResult.KBNumber} from {catalogResult.DownloadUrls.First()}");

                var downloadUrl = catalogResult.DownloadUrls.First();
                var downloadResult = NetworkService.DownloadFile(downloadUrl, filePath.FullName, this, null);

                if (downloadResult)
                {
                    package.LocalFile = filePath;
                    package.IsDownloaded = true;
                    package.FileSize = filePath.Length;
                    package.DownloadedAt = DateTime.UtcNow;
                    package.DownloadUrl = downloadUrl;

                    LoggingService.WriteVerbose(this, $"[{currentIndex} of {totalCount}] - Successfully downloaded {catalogResult.KBNumber} to {filePath.FullName}");

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
                LocalFile = new FileInfo(string.Empty),
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
        /// Updates the database with package information
        /// </summary>
        private void UpdateDatabase(List<WindowsUpdatePackage> packages)
        {
            try
            {
                LoggingService.WriteVerbose(this, $"Updating database with download information for {packages.Count} packages");

                // Convert packages back to WindowsUpdate objects for database compatibility
                var updates = packages.Select(ConvertToWindowsUpdate).ToList();

                using var databaseService = new WindowsUpdateDatabaseService();
                var updatedCount = databaseService.StoreUpdates(updates, "Downloaded", this);

                LoggingService.WriteVerbose(this, $"Updated {updatedCount} database records");
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(this, $"Failed to update database: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts WindowsUpdatePackage back to WindowsUpdate for database compatibility
        /// </summary>
        private WindowsUpdate ConvertToWindowsUpdate(WindowsUpdatePackage package)
        {
            return new WindowsUpdate
            {
                UpdateId = package.UpdateId,
                KBNumber = package.KBNumber,
                Title = package.Title,
                Products = string.Join(", ", package.SourceCatalogResult.Products),
                Classification = package.SourceCatalogResult.Classification,
                LastUpdated = package.SourceCatalogResult.LastModified,
                SizeInBytes = package.SourceCatalogResult.Size,
                DownloadUrls = package.SourceCatalogResult.DownloadUrls.ToList(),
                Architecture = package.SourceCatalogResult.Architecture,
                LocalFilePath = package.LocalFile.FullName
            };
        }
    }
}
