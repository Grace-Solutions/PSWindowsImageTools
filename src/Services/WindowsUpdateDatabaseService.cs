using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Management.Automation;
using System.Text.Json;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for managing Windows Update records in the database
    /// </summary>
    public class WindowsUpdateDatabaseService : IDisposable
    {
        private const string ServiceName = "WindowsUpdateDatabaseService";
        private readonly DatabaseService _databaseService;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the Windows Update Database Service
        /// </summary>
        public WindowsUpdateDatabaseService()
        {
            var databasePath = ConfigurationService.DatabasePath;
            _databaseService = new DatabaseService(databasePath);
            InitializeDatabase();
        }

        /// <summary>
        /// Initializes the Windows Update database tables
        /// </summary>
        private void InitializeDatabase()
        {
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS WindowsUpdates (
                    UpdateId TEXT PRIMARY KEY,
                    Title TEXT NOT NULL,
                    KBNumber TEXT,
                    Classification TEXT,
                    Architecture TEXT,
                    LastUpdated TEXT,
                    SizeInBytes INTEGER,
                    SearchQuery TEXT,
                    UpdateData TEXT NOT NULL,
                    DatabaseTimestamp TEXT DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS idx_windowsupdates_kbnumber ON WindowsUpdates(KBNumber);
                CREATE INDEX IF NOT EXISTS idx_windowsupdates_classification ON WindowsUpdates(Classification);
                CREATE INDEX IF NOT EXISTS idx_windowsupdates_architecture ON WindowsUpdates(Architecture);
                CREATE INDEX IF NOT EXISTS idx_windowsupdates_title ON WindowsUpdates(Title);
            ";

            // Execute the SQL using the database service's ExecuteQuery method
            _databaseService.ExecuteQuery(createTableSql);
        }

        /// <summary>
        /// Stores Windows Update records in the database
        /// </summary>
        /// <param name="updates">Updates to store</param>
        /// <param name="searchQuery">Search query used to find these updates</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Number of records stored</returns>
        public int StoreUpdates(List<WindowsUpdate> updates, string searchQuery, PSCmdlet? cmdlet = null)
        {
            if (updates == null || !updates.Any())
            {
                return 0;
            }

            var operationStartTime = LoggingService.LogOperationStartWithTimestamp(cmdlet, ServiceName,
                "Store Windows Updates", $"{updates.Count} updates from query: '{searchQuery}'");

            int storedCount = 0;

            try
            {
                foreach (var update in updates)
                {
                    try
                    {
                        var sql = @"
                            INSERT OR REPLACE INTO WindowsUpdates
                            (UpdateId, Title, KBNumber, Classification, Architecture, LastUpdated, SizeInBytes, SearchQuery, UpdateData, DatabaseTimestamp)
                            VALUES (@UpdateId, @Title, @KBNumber, @Classification, @Architecture, @LastUpdated, @SizeInBytes, @SearchQuery, @UpdateData, @DatabaseTimestamp)";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@UpdateId"] = update.UpdateId,
                            ["@Title"] = update.Title,
                            ["@KBNumber"] = update.KBNumber ?? (object)DBNull.Value,
                            ["@Classification"] = update.Classification,
                            ["@Architecture"] = update.Architecture ?? (object)DBNull.Value,
                            ["@LastUpdated"] = update.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss"),
                            ["@SizeInBytes"] = update.SizeInBytes,
                            ["@SearchQuery"] = searchQuery,
                            ["@UpdateData"] = JsonSerializer.Serialize(update),
                            ["@DatabaseTimestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                        };

                        _databaseService.ExecuteQuery(sql, parameters);
                        storedCount++;
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName,
                            $"Failed to store update {update.UpdateId}: {ex.Message}");
                    }
                }

                LoggingService.LogOperationCompleteWithTimestamp(cmdlet, ServiceName, "Store Windows Updates", operationStartTime,
                    $"Stored {storedCount} of {updates.Count} updates");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to store updates: {ex.Message}", ex);
            }

            return storedCount;
        }



        /// <summary>
        /// Retrieves Windows Updates from the database
        /// </summary>
        /// <param name="criteria">Search criteria</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>List of Windows Updates</returns>
        public List<WindowsUpdate> GetUpdates(WindowsUpdateSearchCriteria criteria, PSCmdlet? cmdlet = null)
        {
            var updates = new List<WindowsUpdate>();

            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Retrieving updates from database with criteria: {criteria.Query}");

                var sql = "SELECT UpdateData FROM WindowsUpdates WHERE 1=1";
                var parameters = new Dictionary<string, object>();

                // Add search filters
                if (!string.IsNullOrEmpty(criteria.Query))
                {
                    sql += " AND (Title LIKE @Query OR KBNumber LIKE @Query)";
                    parameters["@Query"] = $"%{criteria.Query}%";
                }

                if (!string.IsNullOrEmpty(criteria.Classification))
                {
                    sql += " AND Classification LIKE @Classification";
                    parameters["@Classification"] = $"%{criteria.Classification}%";
                }

                if (!string.IsNullOrEmpty(criteria.Architecture))
                {
                    sql += " AND Architecture = @Architecture";
                    parameters["@Architecture"] = criteria.Architecture!;
                }

                // Add pagination
                sql += " LIMIT @PageSize OFFSET @Offset";
                parameters["@PageSize"] = criteria.PageSize;
                parameters["@Offset"] = (criteria.Page - 1) * criteria.PageSize;

                var dataTable = _databaseService.ExecuteQuery(sql, parameters);

                foreach (DataRow row in dataTable.Rows)
                {
                    try
                    {
                        var updateJson = row["UpdateData"].ToString();
                        if (!string.IsNullOrEmpty(updateJson))
                        {
                            var update = JsonSerializer.Deserialize<WindowsUpdate>(updateJson);
                            if (update != null)
                            {
                                updates.Add(update);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        LoggingService.WriteWarning(cmdlet, ServiceName, $"Failed to deserialize update: {ex.Message}");
                    }
                }

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Retrieved {updates.Count} updates from database");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to retrieve updates from database: {ex.Message}", ex);
            }

            return updates;
        }

        /// <summary>
        /// Gets an update by its ID
        /// </summary>
        /// <param name="updateId">Update ID</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Windows Update or null if not found</returns>
        public WindowsUpdate? GetUpdateById(string updateId, PSCmdlet? cmdlet = null)
        {
            try
            {
                var sql = "SELECT UpdateData FROM WindowsUpdates WHERE UpdateId = @UpdateId";
                var parameters = new Dictionary<string, object>
                {
                    ["@UpdateId"] = updateId
                };

                var dataTable = _databaseService.ExecuteQuery(sql, parameters);

                if (dataTable.Rows.Count > 0)
                {
                    var updateJson = dataTable.Rows[0]["UpdateData"].ToString();
                    if (!string.IsNullOrEmpty(updateJson))
                    {
                        return JsonSerializer.Deserialize<WindowsUpdate>(updateJson);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to get update {updateId}: {ex.Message}", ex);
            }

            return null;
        }



        /// <summary>
        /// Clears old update records from the database
        /// </summary>
        /// <param name="olderThanDays">Remove records older than this many days</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>Number of records removed</returns>
        public int CleanupOldRecords(int olderThanDays, PSCmdlet? cmdlet = null)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
                var sql = "DELETE FROM WindowsUpdates WHERE DatabaseTimestamp < @CutoffDate";
                var parameters = new Dictionary<string, object>
                {
                    ["@CutoffDate"] = cutoffDate.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var result = _databaseService.ExecuteQuery(sql, parameters);
                var deletedCount = result.Rows.Count; // This is an approximation

                LoggingService.WriteVerbose(cmdlet, ServiceName,
                    $"Cleaned up Windows Update records older than {olderThanDays} days");

                return deletedCount;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to cleanup old records: {ex.Message}", ex);
                return 0;
            }
        }

        /// <summary>
        /// Executes a custom SQL query and returns the results as a DataTable
        /// </summary>
        /// <param name="query">SQL query to execute</param>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <returns>DataTable with query results</returns>
        public DataTable ExecuteQuery(string query, PSCmdlet? cmdlet = null)
        {
            try
            {
                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Executing custom query: {query}");

                var result = _databaseService.ExecuteQuery(query);

                LoggingService.WriteVerbose(cmdlet, ServiceName, $"Query returned {result.Rows.Count} rows");

                return result;
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(cmdlet, ServiceName, $"Failed to execute query: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Disposes the service
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _databaseService?.Dispose();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
