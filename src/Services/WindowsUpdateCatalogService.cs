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
    /// Represents a catalog session with ViewState management for proper ASP.NET interaction
    /// </summary>
    public class CatalogSession
    {
        public string ViewState { get; set; } = string.Empty;
        public string ViewStateGenerator { get; set; } = string.Empty;
        public string EventValidation { get; set; } = string.Empty;
        public string EventArgument { get; set; } = string.Empty;
        public bool HasNextPage { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
    }

    /// <summary>
    /// Result of a catalog HTTP request
    /// </summary>
    public class CatalogRequestResult
    {
        public bool Success { get; set; }
        public string Html { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of parsing a catalog response
    /// </summary>
    public class CatalogParseResult
    {
        public bool NoResults { get; set; }
        public List<WindowsUpdate> Updates { get; set; } = new List<WindowsUpdate>();
    }

    /// <summary>
    /// Result of applying sorting to catalog results
    /// </summary>
    public class CatalogSortResult
    {
        public bool Success { get; set; }
        public List<WindowsUpdate> Updates { get; set; } = new List<WindowsUpdate>();
        public CatalogSession Session { get; set; } = new CatalogSession();
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service for searching and retrieving updates from the Microsoft Windows Update Catalog
    /// Based on MSCatalog module analysis with proper ViewState management and sorting
    /// </summary>
    public class WindowsUpdateCatalogService : IDisposable
    {
        private const string ServiceName = "WindowsUpdateCatalogService";
        private const string CatalogBaseUrl = "https://www.catalog.update.microsoft.com";
        private const string SearchUrl = CatalogBaseUrl + "/Search.aspx";
        private const string DownloadDialogUrl = CatalogBaseUrl + "/DownloadDialog.aspx";

        // Table and element IDs from MSCatalog analysis
        private const string ResultsTableId = "ctl00_catalogBody_updateMatches";
        private const string NoResultsElementId = "ctl00_catalogBody_noResultText";
        private const string ViewStateId = "__VIEWSTATE";
        private const string ViewStateGeneratorId = "__VIEWSTATEGENERATOR";
        private const string EventValidationId = "__EVENTVALIDATION";

        // Sort field constants
        private const string SortFieldLastUpdated = "LastUpdated";

        private readonly HttpClient _httpClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the Windows Update Catalog Service
        /// </summary>
        public WindowsUpdateCatalogService()
        {
            // Create HttpClientHandler with automatic decompression
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };

            _httpClient = new HttpClient(handler);

            // Configure HttpClient to mimic browser behavior (required by catalog)
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Searches the Windows Update Catalog for updates with proper ViewState management
        /// </summary>
        /// <param name="criteria">Search criteria</param>
        /// <param name="includeDownloadUrls">Whether to include download URLs (optional, causes extra requests)</param>
        /// <param name="debugMode">Enable debug mode with detailed HTTP logging and global variables</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Search results with session state and warnings</returns>
        public WindowsUpdateSearchResult SearchUpdates(WindowsUpdateSearchCriteria criteria, bool includeDownloadUrls = false, bool debugMode = false, PSCmdlet? cmdlet = null)
        {
            var result = new WindowsUpdateSearchResult
            {
                Criteria = criteria,
                CurrentPage = criteria.Page
            };

            var session = new CatalogSession();
            var searchStartTime = LoggingService.LogOperationStartWithTimestamp(cmdlet, ServiceName,
                "Windows Update Catalog Search", $"Query: '{criteria.Query}'");

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Searching Windows Update Catalog: '{criteria.Query}'");

                // Step 1: Initial search request
                var searchUrl = BuildSearchUrl(criteria);
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Search URL: {searchUrl}");

                var searchResponse = PerformCatalogRequest(searchUrl, "GET", null, debugMode, cmdlet);
                if (!searchResponse.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = searchResponse.ErrorMessage;
                    session.Warnings.Add($"Initial search failed: {searchResponse.ErrorMessage}");
                    return result;
                }

                // Step 2: Parse initial response and extract ViewState
                var parseResult = ParseCatalogResponse(searchResponse.Html, session, debugMode, cmdlet);
                if (parseResult.NoResults)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"No results found for query: {criteria.Query}");
                    result.Updates = new List<WindowsUpdate>();
                    result.Success = true;
                    return result;
                }

                // Step 3: Apply default sorting (LastUpdated descending) if no specific sort requested
                var updates = parseResult.Updates;
                if (!string.IsNullOrEmpty(criteria.SortBy) || !string.IsNullOrEmpty(criteria.SortDirection))
                {
                    var sortResult = ApplySorting(searchUrl, session, criteria, cmdlet);
                    if (sortResult.Success)
                    {
                        updates = sortResult.Updates;
                        session = sortResult.Session;
                    }
                    else
                    {
                        session.Warnings.Add($"Sorting failed: {sortResult.ErrorMessage}");
                    }
                }

                // Step 4: Include download URLs if requested
                if (includeDownloadUrls)
                {
                    IncludeDownloadUrls(updates, session, cmdlet);
                }

                // Step 5: Apply additional filtering and pagination
                var filteredUpdates = ApplyFilters(updates, criteria, cmdlet);
                result.Updates = filteredUpdates;
                result.TotalCount = updates.Count;
                result.TotalPages = (int)Math.Ceiling((double)updates.Count / criteria.PageSize);
                result.Success = true;

                LoggingService.LogOperationCompleteWithTimestamp(cmdlet, ServiceName, "Windows Update Catalog Search", searchStartTime,
                    $"Found {result.Updates.Count} updates (Total: {result.TotalCount})");

                // Add any warnings to the result
                if (session.Warnings.Any())
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Search completed with {session.Warnings.Count} warnings");
                    foreach (var warning in session.Warnings)
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"Warning: {warning}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                session.Warnings.Add($"Search exception: {ex.Message}");
                LoggingService.WriteError(cmdlet, ServiceName, $"Windows Update Catalog search failed: {ex.Message}", ex);
            }
            finally
            {
                result.SearchDuration = DateTime.UtcNow - searchStartTime;
            }

            return result;
        }

        /// <summary>
        /// Performs a catalog request with proper error handling and debug logging
        /// </summary>
        private CatalogRequestResult PerformCatalogRequest(string url, string method, Dictionary<string, string>? formData, bool debugMode, PSCmdlet? cmdlet)
        {
            var result = new CatalogRequestResult();

            try
            {
                if (debugMode)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Making {method} request to: {url}");
                    if (formData != null && formData.Any())
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Form data: {string.Join(", ", formData.Select(kvp => $"{kvp.Key}={kvp.Value?.Substring(0, Math.Min(100, kvp.Value?.Length ?? 0))}..."))}");
                    }
                }

                HttpResponseMessage response;

                if (method.ToUpper() == "GET")
                {
                    response = _httpClient.GetAsync(url).Result;
                }
                else
                {
                    var content = new FormUrlEncodedContent(formData ?? new Dictionary<string, string>());
                    response = _httpClient.PostAsync(url, content).Result;
                }

                // Debug logging for response
                if (debugMode)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Response Status: {response.StatusCode} ({(int)response.StatusCode})");
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}"))}");

                    // Set global variables for debugging
                    if (cmdlet != null)
                    {
                        cmdlet.SessionState.PSVariable.Set("Global:LastCatalogResponse", response);
                        cmdlet.SessionState.PSVariable.Set("Global:LastCatalogStatusCode", response.StatusCode);
                        cmdlet.SessionState.PSVariable.Set("Global:LastCatalogUrl", url);
                        cmdlet.SessionState.PSVariable.Set("Global:LastCatalogMethod", method);
                    }
                }

                response.EnsureSuccessStatusCode();
                result.Html = response.Content.ReadAsStringAsync().Result;
                result.Success = true;

                // Debug logging for content
                if (debugMode)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Response Content Length: {result.Html.Length} characters");
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Response Content Preview: {result.Html.Substring(0, Math.Min(500, result.Html.Length))}...");

                    // Set global variables for content analysis
                    if (cmdlet != null)
                    {
                        cmdlet.SessionState.PSVariable.Set("Global:LastCatalogHtml", result.Html);
                        cmdlet.SessionState.PSVariable.Set("Global:LastCatalogContentLength", result.Html.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;

                if (debugMode)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Request failed with exception: {ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                    }

                    // Set global variables for error analysis
                    if (cmdlet != null)
                    {
                        cmdlet.SessionState.PSVariable.Set("Global:LastCatalogError", ex);
                        cmdlet.SessionState.PSVariable.Set("Global:LastCatalogErrorMessage", ex.Message);
                    }
                }

                LoggingService.WriteError(cmdlet, ServiceName, $"Catalog request failed: {ex.Message}", ex);
            }

            return result;
        }

        /// <summary>
        /// Parses catalog response and extracts ViewState and updates
        /// </summary>
        private CatalogParseResult ParseCatalogResponse(string html, CatalogSession session, bool debugMode, PSCmdlet? cmdlet)
        {
            var result = new CatalogParseResult();

            try
            {
                if (debugMode)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Starting HTML parsing, content length: {html.Length}");
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                if (debugMode)
                {
                    // Set global variables for HTML analysis
                    if (cmdlet != null)
                    {
                        cmdlet.SessionState.PSVariable.Set("Global:LastCatalogDocument", doc);
                        cmdlet.SessionState.PSVariable.Set("Global:LastCatalogHtmlContent", html);
                    }

                    // Check for various elements
                    var allTables = doc.DocumentNode.SelectNodes("//table");
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Found {allTables?.Count ?? 0} tables in HTML");

                    if (allTables != null)
                    {
                        foreach (var table in allTables)
                        {
                            var id = table.GetAttributeValue("id", "");
                            var className = table.GetAttributeValue("class", "");
                            var rowCount = table.SelectNodes(".//tr")?.Count ?? 0;
                            LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Table: ID='{id}', Class='{className}', Rows={rowCount}");
                        }
                    }
                }

                // Check for "no results" message - element exists but only has content when no results
                var noResultsElement = doc.GetElementbyId(NoResultsElementId);
                if (debugMode)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] No results element found: {noResultsElement != null}");
                    if (noResultsElement != null)
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] No results element text: '{noResultsElement.InnerText?.Trim()}'");
                    }
                }

                if (noResultsElement != null && !string.IsNullOrWhiteSpace(noResultsElement.InnerText))
                {
                    result.NoResults = true;
                    session.Warnings.Add($"No results found: {noResultsElement.InnerText?.Trim()}");
                    return result;
                }

                // Extract ViewState information
                ExtractViewState(doc, session);

                // Find and parse the results table
                var resultsTable = doc.GetElementbyId(ResultsTableId);
                if (debugMode)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Results table found: {resultsTable != null}");
                    if (resultsTable != null)
                    {
                        var tableRows = resultsTable.SelectNodes(".//tr");
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Results table has {tableRows?.Count ?? 0} rows");
                    }
                }

                if (resultsTable == null)
                {
                    session.Warnings.Add("Results table not found in response");
                    result.NoResults = true;
                    return result;
                }

                // Parse update rows (skip header row)
                var allRows = resultsTable.SelectNodes(".//tr");
                var rows = allRows?.Where(r => r.Id != "headerRow" && !string.IsNullOrEmpty(r.Id)).ToList();

                if (debugMode)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Total table rows: {allRows?.Count ?? 0}");
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Data rows (excluding header): {rows?.Count ?? 0}");
                    if (rows != null && rows.Any())
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] First row ID: {rows.First().Id}");
                        var firstRowCellCount = rows.First().SelectNodes(".//td")?.Count ?? 0;
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] First row cell count: {firstRowCellCount}");
                    }
                }

                if (rows == null || !rows.Any())
                {
                    session.Warnings.Add("No update rows found in results table");
                    result.NoResults = true;
                    return result;
                }

                result.Updates = new List<WindowsUpdate>();
                foreach (var row in rows)
                {
                    try
                    {
                        var update = ParseUpdateRow(row, cmdlet);
                        if (update != null)
                        {
                            result.Updates.Add(update);
                            if (debugMode)
                            {
                                LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Parsed update: {update.UpdateId} - {update.Title}");
                            }
                        }
                        else if (debugMode)
                        {
                            LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Failed to parse row: {row.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        session.Warnings.Add($"Failed to parse update row {row.Id}: {ex.Message}");
                        if (debugMode)
                        {
                            LoggingService.WriteVerbose(cmdlet, ServiceName, $"[DEBUG] Exception parsing row {row.Id}: {ex}");
                        }
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Parsed {result.Updates.Count} updates from catalog response");
            }
            catch (Exception ex)
            {
                result.NoResults = true;
                session.Warnings.Add($"Failed to parse catalog response: {ex.Message}");
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to parse catalog response: {ex.Message}", ex);
            }

            return result;
        }

        /// <summary>
        /// Gets download URLs for a specific update
        /// Based on Windows Update Catalog specification
        /// </summary>
        /// <param name="updateId">Update ID</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>List of download URLs as properly formed Uri objects</returns>
        public List<Uri> GetDownloadUrls(string updateId, PSCmdlet? cmdlet = null)
        {
            var downloadUrls = new List<Uri>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Getting download URLs for update: {updateId}");

                // Use specification format: POST form data to DownloadDialog.aspx
                var postData = new Dictionary<string, string>
                {
                    ["updateIDs"] = $"[{{\"size\":0,\"updateID\":\"{updateId}\",\"sku\":\"\"}}]",
                    ["sku"] = ""
                };

                var downloadUrl = $"{CatalogBaseUrl}/DownloadDialog.aspx";
                var requestResult = PerformCatalogRequest(downloadUrl, "POST", postData, false, cmdlet);

                if (requestResult.Success)
                {
                    downloadUrls = ParseDownloadUrls(requestResult.Html, cmdlet);
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Found {downloadUrls.Count} download URLs for update {updateId}");
                }
                else
                {
                    LoggingService.WriteError(cmdlet, ServiceName, $"Failed to get download URLs for update {updateId}: {requestResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to get download URLs for update {updateId}: {ex.Message}", ex);
            }

            return downloadUrls;
        }

        /// <summary>
        /// Extracts ViewState information from HTML document
        /// </summary>
        private void ExtractViewState(HtmlDocument doc, CatalogSession session)
        {
            try
            {
                var viewStateElement = doc.GetElementbyId(ViewStateId);
                if (viewStateElement != null)
                {
                    session.ViewState = viewStateElement.GetAttributeValue("value", "");
                }

                var viewStateGeneratorElement = doc.GetElementbyId(ViewStateGeneratorId);
                if (viewStateGeneratorElement != null)
                {
                    session.ViewStateGenerator = viewStateGeneratorElement.GetAttributeValue("value", "");
                }

                var eventValidationElement = doc.GetElementbyId(EventValidationId);
                if (eventValidationElement != null)
                {
                    session.EventValidation = eventValidationElement.GetAttributeValue("value", "");
                }

                // Check for next page link
                var nextPageElement = doc.GetElementbyId("ctl00_catalogBody_nextPage");
                session.HasNextPage = nextPageElement != null;
            }
            catch (Exception ex)
            {
                session.Warnings.Add($"Failed to extract ViewState: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies sorting to catalog results using ViewState
        /// </summary>
        private CatalogSortResult ApplySorting(string baseUrl, CatalogSession session, WindowsUpdateSearchCriteria criteria, PSCmdlet? cmdlet)
        {
            var result = new CatalogSortResult { Session = session };

            try
            {
                var sortBy = criteria.SortBy ?? SortFieldLastUpdated;
                var descending = (criteria.SortDirection?.Equals("Descending", StringComparison.OrdinalIgnoreCase) ?? true) ||
                                (string.IsNullOrEmpty(criteria.SortDirection) && sortBy == SortFieldLastUpdated); // Default LastUpdated to descending

                var eventTarget = GetSortEventTarget(sortBy);
                if (string.IsNullOrEmpty(eventTarget))
                {
                    result.ErrorMessage = $"Unknown sort field: {sortBy}";
                    return result;
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Applying sort: {sortBy} ({(descending ? "descending" : "ascending")})");

                // First sort request
                var sortData = new Dictionary<string, string>
                {
                    ["__EVENTARGUMENT"] = session.EventArgument,
                    ["__EVENTTARGET"] = eventTarget,
                    ["__EVENTVALIDATION"] = session.EventValidation,
                    ["__VIEWSTATE"] = session.ViewState,
                    ["__VIEWSTATEGENERATOR"] = session.ViewStateGenerator
                };

                var sortResponse = PerformCatalogRequest(baseUrl, "POST", sortData, false, cmdlet);
                if (!sortResponse.Success)
                {
                    result.ErrorMessage = sortResponse.ErrorMessage;
                    return result;
                }

                var parseResult = ParseCatalogResponse(sortResponse.Html, session, false, cmdlet);
                if (parseResult.NoResults)
                {
                    result.ErrorMessage = "No results after sorting";
                    return result;
                }

                // Handle double-sort for specific cases (based on MSCatalog logic)
                if ((sortBy == SortFieldLastUpdated && !descending) || (sortBy != SortFieldLastUpdated && descending))
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Applying second sort request for proper order");

                    var secondSortResponse = PerformCatalogRequest(baseUrl, "POST", sortData, false, cmdlet);
                    if (secondSortResponse.Success)
                    {
                        var secondParseResult = ParseCatalogResponse(secondSortResponse.Html, session, false, cmdlet);
                        if (!secondParseResult.NoResults)
                        {
                            parseResult = secondParseResult;
                        }
                    }
                }

                result.Updates = parseResult.Updates;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                session.Warnings.Add($"Sorting failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Gets the EventTarget for sorting based on field name
        /// </summary>
        private static string GetSortEventTarget(string sortBy)
        {
            return sortBy switch
            {
                "Title" => "ctl00$catalogBody$updateMatches$ctl02$titleHeaderLink",
                "Products" => "ctl00$catalogBody$updateMatches$ctl02$productsHeaderLink",
                "Classification" => "ctl00$catalogBody$updateMatches$ctl02$classificationComputedHeaderLink",
                SortFieldLastUpdated => "ctl00$catalogBody$updateMatches$ctl02$dateComputedHeaderLink",
                "Version" => "ctl00$catalogBody$updateMatches$ctl02$driverVerVersionHeaderLink",
                "Size" => "ctl00$catalogBody$updateMatches$ctl02$sizeInBytesHeaderLink",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Includes download URLs for updates (optional, causes extra requests)
        /// </summary>
        private void IncludeDownloadUrls(List<WindowsUpdate> updates, CatalogSession session, PSCmdlet? cmdlet)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, $"Including download URLs for {updates.Count} updates");

            foreach (var update in updates)
            {
                try
                {
                    if (!string.IsNullOrEmpty(update.UpdateId))
                    {
                        var uris = GetDownloadUrls(update.UpdateId, cmdlet);
                        update.DownloadUrls = uris.Select(uri => uri.OriginalString).ToList();
                    }
                }
                catch (Exception ex)
                {
                    session.Warnings.Add($"Failed to get download URLs for update {update.UpdateId}: {ex.Message}");
                }
            }
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
        /// Parses a single update row from the results table
        /// Based on Windows Update Catalog specification:
        /// C0: Icon/Checkbox (skip), C1: Title, C2: Products, C3: Classification,
        /// C4: Last Updated, C5: Version, C6: Size, C7: Download
        /// </summary>
        private WindowsUpdate? ParseUpdateRow(HtmlNode row, PSCmdlet? cmdlet)
        {
            try
            {
                var cells = row.SelectNodes(".//td");
                if (cells == null || cells.Count < 7)
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Row has insufficient columns: {cells?.Count ?? 0} (need at least 7)");
                    return null; // Need at least 7 columns (C0-C6)
                }

                var update = new WindowsUpdate();

                // Extract update ID from row ID attribute (format: {uuid}_R{index})
                var rowId = row.GetAttributeValue("id", "");
                if (!string.IsNullOrEmpty(rowId))
                {
                    var updateIdMatch = Regex.Match(rowId, @"^([a-f0-9\-]+)_R\d+$", RegexOptions.IgnoreCase);
                    if (updateIdMatch.Success)
                    {
                        update.UpdateId = updateIdMatch.Groups[1].Value;
                    }
                    else
                    {
                        LoggingService.WriteVerbose(cmdlet, ServiceName, $"Could not extract update ID from row ID: {rowId}");
                    }
                }
                else
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, "Row has no ID attribute");
                }

                // C1: Title (second cell - skip C0 which is icon/checkbox)
                var titleCell = cells[1];
                var titleLink = titleCell.SelectSingleNode(".//a");
                if (titleLink != null)
                {
                    update.Title = HtmlEntity.DeEntitize(titleLink.InnerText?.Trim() ?? "");
                }
                else
                {
                    // Fallback to cell text if no link found
                    update.Title = HtmlEntity.DeEntitize(titleCell.InnerText?.Trim() ?? "");
                }

                // C2: Products (third cell) - Parse and clean up products list
                var productsText = HtmlEntity.DeEntitize(cells[2].InnerText?.Trim() ?? "");
                update.ProductsList = ParseProductsList(productsText);
                update.Products = string.Join(", ", update.ProductsList);

                // C3: Classification (fourth cell)
                update.Classification = HtmlEntity.DeEntitize(cells[3].InnerText?.Trim() ?? "");

                // C4: Last Updated (fifth cell) - Parse using MM/dd/yyyy format
                var lastUpdatedText = cells[4].InnerText?.Trim() ?? "";
                if (!string.IsNullOrEmpty(lastUpdatedText))
                {
                    // Try MSCatalog date parsing approach first (MM/dd/yyyy)
                    if (TryParseCatalogDate(lastUpdatedText, out DateTime catalogDate))
                    {
                        update.LastUpdated = catalogDate;
                    }
                    else if (DateTime.TryParse(lastUpdatedText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fallbackDate))
                    {
                        update.LastUpdated = fallbackDate;
                    }
                }

                // C5: Version (sixth cell)
                update.Version = HtmlEntity.DeEntitize(cells[5].InnerText?.Trim() ?? "");
                if (update.Version == "n/a") update.Version = string.Empty;

                // C6: Size (seventh cell) - Handle both human-readable and hidden byte count
                var sizeCell = cells[6];
                var sizeText = sizeCell.InnerText?.Trim() ?? "";
                update.SizeFormatted = sizeText;

                // Try to extract hidden byte count first (more accurate)
                var hiddenSizeElement = sizeCell.SelectSingleNode(".//span[@style='display:none']");
                if (hiddenSizeElement != null && long.TryParse(hiddenSizeElement.InnerText?.Trim(), out long exactBytes))
                {
                    update.SizeInBytes = exactBytes;
                }
                else
                {
                    // Fallback to parsing human-readable size
                    var sizeMatch = Regex.Match(sizeText, @"([\d,\.]+)\s*([KMGT]?B)", RegexOptions.IgnoreCase);
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
                }

                // Extract KB number from title
                var kbMatch = Regex.Match(update.Title, @"\(KB(\d+)\)", RegexOptions.IgnoreCase);
                if (kbMatch.Success)
                {
                    update.KBNumber = "KB" + kbMatch.Groups[1].Value;
                }

                // Determine architecture from title/products with better patterns
                var titleAndProducts = $"{update.Title} {update.Products}".ToLowerInvariant();
                if (titleAndProducts.Contains("arm64") || titleAndProducts.Contains("arm-based"))
                    update.Architecture = "ARM64";
                else if (titleAndProducts.Contains("x64") || titleAndProducts.Contains("amd64") || titleAndProducts.Contains("64-bit"))
                    update.Architecture = "x64";
                else if (titleAndProducts.Contains("x86") || titleAndProducts.Contains("32-bit"))
                    update.Architecture = "x86";
                else
                    update.Architecture = "Unknown";

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
        /// Based on Windows Update Catalog specification - extracts URLs from JavaScript downloadInformation variable
        /// </summary>
        private static List<Uri> ParseDownloadUrls(string html, PSCmdlet? cmdlet)
        {
            var urls = new List<Uri>();

            try
            {
                // Primary method: Extract from JavaScript downloadInformation variable as per specification
                var urlPattern = @"\.url\s*=\s*'([^']+)'";
                var urlMatches = Regex.Matches(html, urlPattern, RegexOptions.IgnoreCase);

                foreach (Match match in urlMatches)
                {
                    if (match.Success && Uri.IsWellFormedUriString(match.Groups[1].Value, UriKind.Absolute))
                    {
                        urls.Add(new Uri(match.Groups[1].Value));
                    }
                }

                // Fallback method: Use broader regex for Microsoft domains
                if (urls.Count == 0)
                {
                    var cleanedHtml = html.Replace("www.download.windowsupdate", "download.windowsupdate");
                    var fallbackRegex = new Regex(@"(https?://(?:catalog\.sf\.)?dl\.delivery\.mp\.microsoft\.com/[^'""]*|https?://(?:catalog\.s\.)?download\.windowsupdate\.com/[^'""]*)", RegexOptions.IgnoreCase);
                    var fallbackMatches = fallbackRegex.Matches(cleanedHtml);

                    foreach (Match match in fallbackMatches)
                    {
                        if (match.Success && Uri.IsWellFormedUriString(match.Value, UriKind.Absolute))
                        {
                            urls.Add(new Uri(match.Value));
                        }
                    }
                }

                // Last resort: HTML parsing for any HTTP links
                if (urls.Count == 0)
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var downloadLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, 'http')]");
                    if (downloadLinks != null)
                    {
                        foreach (var link in downloadLinks)
                        {
                            var href = link.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href) && Uri.IsWellFormedUriString(href, UriKind.Absolute))
                            {
                                urls.Add(new Uri(href));
                            }
                        }
                    }
                }

                // Remove duplicates based on absolute URI
                urls = urls.Distinct().ToList();
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to parse download URLs: {ex.Message}", ex);
            }

            return urls;
        }

        /// <summary>
        /// Tries to parse date using MSCatalog approach (MM/dd/yyyy format)
        /// </summary>
        private static bool TryParseCatalogDate(string dateString, out DateTime date)
        {
            date = default;

            try
            {
                // MSCatalog uses MM/dd/yyyy format
                var parts = dateString.Split('/');
                if (parts.Length == 3 &&
                    int.TryParse(parts[0], out int month) &&
                    int.TryParse(parts[1], out int day) &&
                    int.TryParse(parts[2], out int year))
                {
                    date = new DateTime(year, month, day);
                    return true;
                }
            }
            catch
            {
                // Fall through to return false
            }

            return false;
        }

        /// <summary>
        /// Parses a products string into a list of unique, trimmed product names
        /// Handles the fact that some product names may contain commas
        /// Based on actual Windows Update Catalog format: "Product1, Product2, Product3"
        /// </summary>
        private static List<string> ParseProductsList(string productsText)
        {
            if (string.IsNullOrWhiteSpace(productsText))
                return new List<string>();

            var products = new List<string>();

            // The catalog uses ", " (comma + space) as the primary separator
            // But we need to be smart about product names that contain commas
            var potentialProducts = productsText.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

            // Clean up each product name
            foreach (var product in potentialProducts)
            {
                var cleaned = product.Trim()
                    .Replace("\u00A0", " ") // Replace non-breaking spaces
                    .Replace("  ", " ")     // Replace double spaces
                    .Trim();

                if (!string.IsNullOrEmpty(cleaned) && cleaned.Length > 1)
                {
                    products.Add(cleaned);
                }
            }

            // Remove duplicates while preserving order
            var uniqueProducts = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var product in products)
            {
                if (seen.Add(product))
                {
                    uniqueProducts.Add(product);
                }
            }

            return uniqueProducts;
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
