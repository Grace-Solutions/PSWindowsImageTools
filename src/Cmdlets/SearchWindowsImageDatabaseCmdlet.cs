using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Searches the local Windows Image database for previously stored data
    /// </summary>
    [Cmdlet(VerbsCommon.Search, "WindowsImageDatabase")]
    [OutputType(typeof(DataTable))]
    public class SearchWindowsImageDatabaseCmdlet : PSCmdlet
    {
        /// <summary>
        /// Type of object to search for using predefined queries
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ByObjectType")]
        [ValidateSet("Updates", "Images", "Operations", "Inventory")]
        public string ObjectType { get; set; } = null!;

        /// <summary>
        /// Custom SQL query to execute
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "CustomQuery")]
        [ValidateNotNullOrEmpty]
        public string CustomQuery { get; set; } = null!;

        /// <summary>
        /// Architecture filter (for Updates object type)
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ByObjectType")]
        [ValidateSet("x86", "AMD64", "ARM64")]
        public string? Architecture { get; set; }

        /// <summary>
        /// Classification filter (for Updates object type)
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ByObjectType")]
        public string? Classification { get; set; }

        /// <summary>
        /// Product filter (for Updates object type)
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ByObjectType")]
        public string? Product { get; set; }

        /// <summary>
        /// KB Number filter (for Updates object type)
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ByObjectType")]
        public string? KBNumber { get; set; }

        /// <summary>
        /// Date range start (for all object types)
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ByObjectType")]
        public DateTime? DateFrom { get; set; }

        /// <summary>
        /// Date range end (for all object types)
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ByObjectType")]
        public DateTime? DateTo { get; set; }

        /// <summary>
        /// Maximum number of results to return
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateRange(1, 10000)]
        public int MaxResults { get; set; } = 1000;

        /// <summary>
        /// Include only downloaded updates (for Updates object type)
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ByObjectType")]
        public SwitchParameter DownloadedOnly { get; set; }

        private const string ComponentName = "WindowsImageDatabase";

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                // Check if database is enabled
                if (ConfigurationService.IsDatabaseDisabled)
                {
                    WriteWarning("Database functionality is disabled. Enable it with Set-WindowsImageDataConfig -EnableDatabase");
                    return;
                }

                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName,
                    "Search Windows Image Database", $"ObjectType: {ObjectType ?? "Custom Query"}");

                LoggingService.WriteVerbose(this, $"Searching database with {ParameterSetName}");

                DataTable results;

                if (ParameterSetName == "CustomQuery")
                {
                    results = ExecuteCustomQuery();
                }
                else
                {
                    results = ExecutePredefinedQuery();
                }

                // Apply row limit
                if (results.Rows.Count > MaxResults)
                {
                    WriteWarning($"Found {results.Rows.Count} results, limiting to {MaxResults}. Use -MaxResults to increase limit.");
                    
                    // Create a new DataTable with limited rows
                    var limitedResults = results.Clone();
                    for (int i = 0; i < MaxResults; i++)
                    {
                        limitedResults.ImportRow(results.Rows[i]);
                    }
                    results = limitedResults;
                }

                // Output results
                WriteObject(results);

                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Search Windows Image Database", operationStartTime,
                    $"Found {results.Rows.Count} results");

                LoggingService.WriteVerbose(this, $"Database search completed: {results.Rows.Count} results returned");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, $"Database search failed: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Executes a custom SQL query
        /// </summary>
        private DataTable ExecuteCustomQuery()
        {
            LoggingService.WriteVerbose(this, $"Executing custom query: {CustomQuery}");

            using var databaseService = new WindowsUpdateDatabaseService();
            return databaseService.ExecuteQuery(CustomQuery, this);
        }

        /// <summary>
        /// Executes a predefined query based on ObjectType
        /// </summary>
        private DataTable ExecutePredefinedQuery()
        {
            var query = BuildPredefinedQuery();
            LoggingService.WriteVerbose(this, $"Executing predefined query for {ObjectType}: {query}");

            using var databaseService = new WindowsUpdateDatabaseService();
            return databaseService.ExecuteQuery(query, this);
        }

        /// <summary>
        /// Builds a predefined query based on ObjectType and filters
        /// </summary>
        private string BuildPredefinedQuery()
        {
            switch (ObjectType.ToLowerInvariant())
            {
                case "updates":
                    return BuildUpdatesQuery();
                
                case "images":
                    return BuildImagesQuery();
                
                case "operations":
                    return BuildOperationsQuery();
                
                case "inventory":
                    return BuildInventoryQuery();
                
                default:
                    throw new ArgumentException($"Unknown ObjectType: {ObjectType}");
            }
        }

        /// <summary>
        /// Builds query for Updates object type
        /// </summary>
        private string BuildUpdatesQuery()
        {
            var query = "SELECT * FROM Updates WHERE 1=1";
            var conditions = new List<string>();

            if (!string.IsNullOrEmpty(Architecture))
            {
                conditions.Add($"Architecture = '{Architecture}'");
            }

            if (!string.IsNullOrEmpty(Classification))
            {
                conditions.Add($"Classification LIKE '%{Classification}%'");
            }

            if (!string.IsNullOrEmpty(Product))
            {
                conditions.Add($"Products LIKE '%{Product}%'");
            }

            if (!string.IsNullOrEmpty(KBNumber))
            {
                conditions.Add($"KBNumber = '{KBNumber}'");
            }

            if (DateFrom.HasValue)
            {
                conditions.Add($"DatabaseTimestamp >= '{DateFrom.Value:yyyy-MM-dd HH:mm:ss}'");
            }

            if (DateTo.HasValue)
            {
                conditions.Add($"DatabaseTimestamp <= '{DateTo.Value:yyyy-MM-dd HH:mm:ss}'");
            }

            if (DownloadedOnly.IsPresent)
            {
                conditions.Add("LocalFilePath IS NOT NULL AND LocalFilePath != ''");
            }

            if (conditions.Any())
            {
                query += " AND " + string.Join(" AND ", conditions);
            }

            query += " ORDER BY DatabaseTimestamp DESC";
            
            if (MaxResults > 0)
            {
                query += $" LIMIT {MaxResults}";
            }

            return query;
        }

        /// <summary>
        /// Builds query for Images object type
        /// </summary>
        private string BuildImagesQuery()
        {
            var query = "SELECT * FROM Images WHERE 1=1";
            var conditions = new List<string>();

            if (DateFrom.HasValue)
            {
                conditions.Add($"ProcessedAt >= '{DateFrom.Value:yyyy-MM-dd HH:mm:ss}'");
            }

            if (DateTo.HasValue)
            {
                conditions.Add($"ProcessedAt <= '{DateTo.Value:yyyy-MM-dd HH:mm:ss}'");
            }

            if (conditions.Any())
            {
                query += " AND " + string.Join(" AND ", conditions);
            }

            query += " ORDER BY ProcessedAt DESC";
            
            if (MaxResults > 0)
            {
                query += $" LIMIT {MaxResults}";
            }

            return query;
        }

        /// <summary>
        /// Builds query for Operations object type
        /// </summary>
        private string BuildOperationsQuery()
        {
            var query = "SELECT * FROM Operations WHERE 1=1";
            var conditions = new List<string>();

            if (DateFrom.HasValue)
            {
                conditions.Add($"StartTime >= '{DateFrom.Value:yyyy-MM-dd HH:mm:ss}'");
            }

            if (DateTo.HasValue)
            {
                conditions.Add($"EndTime <= '{DateTo.Value:yyyy-MM-dd HH:mm:ss}'");
            }

            if (conditions.Any())
            {
                query += " AND " + string.Join(" AND ", conditions);
            }

            query += " ORDER BY StartTime DESC";
            
            if (MaxResults > 0)
            {
                query += $" LIMIT {MaxResults}";
            }

            return query;
        }

        /// <summary>
        /// Builds query for Inventory object type
        /// </summary>
        private string BuildInventoryQuery()
        {
            var query = "SELECT * FROM Inventory WHERE 1=1";
            var conditions = new List<string>();

            if (DateFrom.HasValue)
            {
                conditions.Add($"Timestamp >= '{DateFrom.Value:yyyy-MM-dd HH:mm:ss}'");
            }

            if (DateTo.HasValue)
            {
                conditions.Add($"Timestamp <= '{DateTo.Value:yyyy-MM-dd HH:mm:ss}'");
            }

            if (conditions.Any())
            {
                query += " AND " + string.Join(" AND ", conditions);
            }

            query += " ORDER BY Timestamp DESC";
            
            if (MaxResults > 0)
            {
                query += $" LIMIT {MaxResults}";
            }

            return query;
        }
    }
}
