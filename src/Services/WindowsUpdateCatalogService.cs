using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for searching and retrieving updates from the Microsoft Windows Update Catalog
    /// </summary>
    public class WindowsUpdateCatalogService : IDisposable
    {
        private const string ServiceName = "WindowsUpdateCatalogService";
        private const string CatalogBaseUrl = "https://www.catalog.update.microsoft.com";
        private const string SearchUrl = CatalogBaseUrl + "/Search.aspx";
        
        private readonly HttpClient _httpClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the Windows Update Catalog Service
        /// </summary>
        public WindowsUpdateCatalogService()
        {
            _httpClient = new HttpClient();
            
            // Configure HttpClient to mimic Internet Explorer (required by catalog)
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0)");
            _httpClient.DefaultRequestHeaders.Add("Accept", 
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Searches the Windows Update Catalog for updates
        /// </summary>
        /// <param name="criteria">Search criteria</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Search results with pagination</returns>
        public WindowsUpdateSearchResult SearchUpdates(WindowsUpdateSearchCriteria criteria, PSCmdlet? cmdlet = null)
        {
            var result = new WindowsUpdateSearchResult
            {
                Criteria = criteria,
                CurrentPage = criteria.Page
            };

            var searchStartTime = LoggingService.LogOperationStartWithTimestamp(cmdlet, ServiceName,
                "Windows Update Catalog Search", $"Query: '{criteria.Query}', Page: {criteria.Page}");

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Searching Windows Update Catalog: '{criteria.Query}' (Page {criteria.Page})");

                // Build search URL with parameters
                var searchUrl = BuildSearchUrl(criteria);
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Search URL: {searchUrl}");

                // Perform the search request
                var response = _httpClient.GetAsync(searchUrl).Result;
                response.EnsureSuccessStatusCode();

                var html = response.Content.ReadAsStringAsync().Result;
                
                // Parse the HTML response
                var updates = ParseSearchResults(html, cmdlet);
                
                // Apply additional filtering
                var filteredUpdates = ApplyFilters(updates, criteria, cmdlet);
                
                // Apply pagination
                var paginatedUpdates = ApplyPagination(filteredUpdates, criteria, out int totalCount, out int totalPages);

                result.Updates = paginatedUpdates;
                result.TotalCount = totalCount;
                result.TotalPages = totalPages;
                result.Success = true;

                LoggingService.LogOperationCompleteWithTimestamp(cmdlet, ServiceName, "Windows Update Catalog Search", searchStartTime,
                    $"Found {result.Updates.Count} updates on page {criteria.Page} of {totalPages} (Total: {totalCount})");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                LoggingService.WriteError(cmdlet, ServiceName, $"Windows Update Catalog search failed: {ex.Message}", ex);
            }
            finally
            {
                result.SearchDuration = DateTime.UtcNow - searchStartTime;
            }

            return result;
        }

        /// <summary>
        /// Gets download URLs for a specific update
        /// </summary>
        /// <param name="updateId">Update ID</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>List of download URLs</returns>
        public List<string> GetDownloadUrls(string updateId, PSCmdlet? cmdlet = null)
        {
            var downloadUrls = new List<string>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Getting download URLs for update: {updateId}");

                // The catalog uses a specific URL pattern for download links
                var downloadUrl = $"{CatalogBaseUrl}/DownloadDialog.aspx";
                var postData = $"updateIDs=%5B%22{updateId}%22%5D";

                var content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");
                var response = _httpClient.PostAsync(downloadUrl, content).Result;
                response.EnsureSuccessStatusCode();

                var html = response.Content.ReadAsStringAsync().Result;
                downloadUrls = ParseDownloadUrls(html, cmdlet);

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found {downloadUrls.Count} download URLs for update {updateId}");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to get download URLs for update {updateId}: {ex.Message}", ex);
            }

            return downloadUrls;
        }

        /// <summary>
        /// Builds the search URL with parameters
        /// </summary>
        private string BuildSearchUrl(WindowsUpdateSearchCriteria criteria)
        {
            var queryBuilder = new StringBuilder(SearchUrl);
            queryBuilder.Append($"?q={Uri.EscapeDataString(criteria.Query)}");

            // Add additional filters if specified
            if (!string.IsNullOrEmpty(criteria.Product))
            {
                queryBuilder.Append($"&product={Uri.EscapeDataString(criteria.Product)}");
            }

            if (!string.IsNullOrEmpty(criteria.Classification))
            {
                queryBuilder.Append($"&classification={Uri.EscapeDataString(criteria.Classification)}");
            }

            return queryBuilder.ToString();
        }

        /// <summary>
        /// Parses search results from HTML
        /// </summary>
        private List<WindowsUpdate> ParseSearchResults(string html, PSCmdlet? cmdlet)
        {
            var updates = new List<WindowsUpdate>();

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Find the results table
                var resultsTable = doc.DocumentNode.SelectSingleNode("//table[@id='ctl00_catalogBody_updateMatches']");
                if (resultsTable == null)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "No results table found in HTML response");
                    return updates;
                }

                // Parse each row (skip header row)
                var rows = resultsTable.SelectNodes(".//tr[position()>1]");
                if (rows == null) return updates;

                foreach (var row in rows)
                {
                    try
                    {
                        var update = ParseUpdateRow(row, cmdlet);
                        if (update != null)
                        {
                            updates.Add(update);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"Failed to parse update row: {ex.Message}");
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Parsed {updates.Count} updates from HTML");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to parse search results: {ex.Message}", ex);
            }

            return updates;
        }

        /// <summary>
        /// Parses a single update row from the results table
        /// </summary>
        private WindowsUpdate? ParseUpdateRow(HtmlNode row, PSCmdlet? cmdlet)
        {
            try
            {
                var cells = row.SelectNodes(".//td");
                if (cells == null || cells.Count < 6) return null;

                var update = new WindowsUpdate();

                // Title (first cell)
                var titleCell = cells[0];
                var titleLink = titleCell.SelectSingleNode(".//a");
                if (titleLink != null)
                {
                    update.Title = HtmlEntity.DeEntitize(titleLink.InnerText?.Trim() ?? "");
                    
                    // Extract update ID from onclick attribute
                    var onclickAttr = titleLink.GetAttributeValue("onclick", "");
                    var updateIdMatch = Regex.Match(onclickAttr, @"'([^']+)'");
                    if (updateIdMatch.Success)
                    {
                        update.UpdateId = updateIdMatch.Groups[1].Value;
                    }
                }

                // Products (second cell)
                update.Products = HtmlEntity.DeEntitize(cells[1].InnerText?.Trim() ?? "");

                // Classification (third cell)
                update.Classification = HtmlEntity.DeEntitize(cells[2].InnerText?.Trim() ?? "");

                // Last Updated (fourth cell)
                var lastUpdatedText = cells[3].InnerText?.Trim() ?? "";
                if (DateTime.TryParse(lastUpdatedText, out DateTime lastUpdated))
                {
                    update.LastUpdated = lastUpdated;
                }

                // Version (fifth cell)
                update.Version = HtmlEntity.DeEntitize(cells[4].InnerText?.Trim() ?? "");

                // Size (sixth cell)
                var sizeText = cells[5].InnerText?.Trim() ?? "";
                update.SizeFormatted = sizeText;
                
                // Parse size in bytes
                var sizeMatch = Regex.Match(sizeText, @"([\d,]+)\s*([KMGT]?B)");
                if (sizeMatch.Success)
                {
                    var sizeValue = sizeMatch.Groups[1].Value.Replace(",", "");
                    var sizeUnit = sizeMatch.Groups[2].Value.ToUpper();
                    
                    if (double.TryParse(sizeValue, out double size))
                    {
                        update.SizeInBytes = sizeUnit switch
                        {
                            "KB" => (long)(size * 1024),
                            "MB" => (long)(size * 1024 * 1024),
                            "GB" => (long)(size * 1024 * 1024 * 1024),
                            "TB" => (long)(size * 1024 * 1024 * 1024 * 1024),
                            _ => (long)size
                        };
                    }
                }

                // Extract KB number from title
                var kbMatch = Regex.Match(update.Title, @"KB(\d+)", RegexOptions.IgnoreCase);
                if (kbMatch.Success)
                {
                    update.KBNumber = kbMatch.Groups[1].Value;
                }

                // Determine architecture from title/products
                if (update.Title.Contains("x64") || update.Products.Contains("x64"))
                    update.Architecture = "x64";
                else if (update.Title.Contains("x86") || update.Products.Contains("x86"))
                    update.Architecture = "x86";
                else if (update.Title.Contains("ARM64") || update.Products.Contains("ARM64"))
                    update.Architecture = "ARM64";

                return update;
            }
            catch (Exception ex)
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Failed to parse update row: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses download URLs from the download dialog HTML
        /// </summary>
        private List<string> ParseDownloadUrls(string html, PSCmdlet? cmdlet)
        {
            var urls = new List<string>();

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Find download links
                var downloadLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, 'http')]");
                if (downloadLinks != null)
                {
                    foreach (var link in downloadLinks)
                    {
                        var href = link.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(href) && Uri.IsWellFormedUriString(href, UriKind.Absolute))
                        {
                            urls.Add(href);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to parse download URLs: {ex.Message}", ex);
            }

            return urls;
        }

        /// <summary>
        /// Applies additional filters to the search results
        /// </summary>
        private List<WindowsUpdate> ApplyFilters(List<WindowsUpdate> updates, WindowsUpdateSearchCriteria criteria, PSCmdlet? cmdlet)
        {
            var filtered = updates.AsEnumerable();

            // Architecture filter
            if (!string.IsNullOrEmpty(criteria.Architecture))
            {
                filtered = filtered.Where(u => u.Architecture?.Equals(criteria.Architecture, StringComparison.OrdinalIgnoreCase) == true);
            }

            // Date range filter
            if (criteria.DateFrom.HasValue)
            {
                filtered = filtered.Where(u => u.LastUpdated >= criteria.DateFrom.Value);
            }

            if (criteria.DateTo.HasValue)
            {
                filtered = filtered.Where(u => u.LastUpdated <= criteria.DateTo.Value);
            }

            // Superseded filter
            if (!criteria.IncludeSuperseded)
            {
                filtered = filtered.Where(u => !u.IsSuperseded);
            }

            var result = filtered.ToList();
            
            if (result.Count != updates.Count)
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Applied filters: {updates.Count} -> {result.Count} updates");
            }

            return result;
        }

        /// <summary>
        /// Applies pagination to the results
        /// </summary>
        private List<WindowsUpdate> ApplyPagination(List<WindowsUpdate> updates, WindowsUpdateSearchCriteria criteria, 
            out int totalCount, out int totalPages)
        {
            totalCount = updates.Count;
            totalPages = (int)Math.Ceiling((double)totalCount / criteria.PageSize);

            var skip = (criteria.Page - 1) * criteria.PageSize;
            return updates.Skip(skip).Take(criteria.PageSize).ToList();
        }

        /// <summary>
        /// Disposes the service
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
