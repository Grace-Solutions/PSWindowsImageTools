using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for managing SQLite database operations with WAL mode and graceful disconnections
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly string _connectionString;
        private SQLiteConnection? _connection;
        private bool _disposed = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Initializes a new instance of the DatabaseService
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        public DatabaseService(string databasePath)
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Connection string with WAL mode and proper settings
            _connectionString = $"Data Source={databasePath};Version=3;DateTimeKind=Utc;Journal Mode=WAL;Synchronous=NORMAL;Cache Size=10000;Foreign Keys=True;";

            // Register for graceful shutdown on process exit
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        /// <summary>
        /// Gets the database connection, creating it if necessary
        /// </summary>
        private SQLiteConnection GetConnection()
        {
            lock (_lockObject)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(DatabaseService));
                }

                if (_connection == null)
                {
                    _connection = new SQLiteConnection(_connectionString);
                    _connection.Open();

                    // Enable WAL mode and optimize settings
                    using (var command = new SQLiteCommand("PRAGMA journal_mode=WAL;", _connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    using (var command = new SQLiteCommand("PRAGMA synchronous=NORMAL;", _connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    using (var command = new SQLiteCommand("PRAGMA cache_size=10000;", _connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    using (var command = new SQLiteCommand("PRAGMA foreign_keys=ON;", _connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                else if (_connection.State != ConnectionState.Open)
                {
                    _connection.Open();
                }

                return _connection;
            }
        }

        /// <summary>
        /// Initializes the database schema with proper timestamp and hash algorithm columns
        /// </summary>
        public void InitializeDatabase()
        {
            var connection = GetConnection();

            // Create Builds table with proper timestamp and hash columns
            var createBuildsTable = @"
                CREATE TABLE IF NOT EXISTS Builds (
                    Id TEXT PRIMARY KEY,
                    SourceImagePath TEXT NOT NULL,
                    SourceImageHash TEXT NOT NULL,
                    SourceImageHashAlgorithm TEXT NOT NULL DEFAULT 'SHA256',
                    OutputImagePath TEXT,
                    OutputImageHash TEXT,
                    OutputImageHashAlgorithm TEXT DEFAULT 'SHA256',
                    RecipeJson TEXT,
                    InventoryJson TEXT,
                    Status TEXT NOT NULL,
                    ErrorMessage TEXT,
                    DurationMs INTEGER DEFAULT 0,
                    ImageCount INTEGER DEFAULT 0,
                    CreatedUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ModifiedUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                )";

            using (var command = new SQLiteCommand(createBuildsTable, connection))
            {
                command.ExecuteNonQuery();
            }

            // Create indexes
            var indexes = new[]
            {
                "CREATE INDEX IF NOT EXISTS idx_builds_status ON Builds(Status)",
                "CREATE INDEX IF NOT EXISTS idx_builds_created ON Builds(CreatedUtc)",
                "CREATE INDEX IF NOT EXISTS idx_builds_hash ON Builds(SourceImageHash)"
            };

            foreach (var indexSql in indexes)
            {
                using (var command = new SQLiteCommand(indexSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Clears all data from all tables
        /// </summary>
        public void ClearAllData()
        {
            var connection = GetConnection();
            using (var command = new SQLiteCommand("DELETE FROM Builds", connection))
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes a custom query and returns results as a DataTable
        /// </summary>
        public DataTable ExecuteQuery(string query, Dictionary<string, object>? parameters = null)
        {
            var connection = GetConnection();
            var dataTable = new DataTable();

            using (var command = new SQLiteCommand(query, connection))
            {
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value);
                    }
                }

                using (var adapter = new SQLiteDataAdapter(command))
                {
                    adapter.Fill(dataTable);
                }
            }

            return dataTable;
        }

        /// <summary>
        /// Bulk inserts data using DataTable for efficient batch operations
        /// </summary>
        /// <param name="tableName">Target table name</param>
        /// <param name="dataTable">DataTable containing the data to insert</param>
        /// <returns>Number of rows inserted</returns>
        public int BulkInsert(string tableName, DataTable dataTable)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
                return 0;

            var connection = GetConnection();
            int rowsInserted = 0;

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Build the INSERT statement dynamically based on DataTable columns
                    var columns = string.Join(", ", dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                    var parameters = string.Join(", ", dataTable.Columns.Cast<DataColumn>().Select(c => $"@{c.ColumnName}"));
                    var insertSql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";

                    using (var command = new SQLiteCommand(insertSql, connection, transaction))
                    {
                        // Add parameters for all columns
                        foreach (DataColumn column in dataTable.Columns)
                        {
                            command.Parameters.Add($"@{column.ColumnName}", DbType.String);
                        }

                        // Insert each row
                        foreach (DataRow row in dataTable.Rows)
                        {
                            for (int i = 0; i < dataTable.Columns.Count; i++)
                            {
                                command.Parameters[i].Value = row[i] ?? DBNull.Value;
                            }

                            command.ExecuteNonQuery();
                            rowsInserted++;
                        }
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }

            return rowsInserted;
        }

        /// <summary>
        /// Creates a DataTable with the schema for the Builds table
        /// </summary>
        /// <returns>DataTable with Builds table schema</returns>
        public DataTable CreateBuildsDataTable()
        {
            var dataTable = new DataTable("Builds");

            dataTable.Columns.Add("Id", typeof(string));
            dataTable.Columns.Add("SourceImagePath", typeof(string));
            dataTable.Columns.Add("SourceImageHash", typeof(string));
            dataTable.Columns.Add("SourceImageHashAlgorithm", typeof(string));
            dataTable.Columns.Add("OutputImagePath", typeof(string));
            dataTable.Columns.Add("OutputImageHash", typeof(string));
            dataTable.Columns.Add("OutputImageHashAlgorithm", typeof(string));
            dataTable.Columns.Add("RecipeJson", typeof(string));
            dataTable.Columns.Add("InventoryJson", typeof(string));
            dataTable.Columns.Add("Status", typeof(string));
            dataTable.Columns.Add("ErrorMessage", typeof(string));
            dataTable.Columns.Add("DurationMs", typeof(int));
            dataTable.Columns.Add("ImageCount", typeof(int));
            dataTable.Columns.Add("CreatedUtc", typeof(DateTime));
            dataTable.Columns.Add("ModifiedUtc", typeof(DateTime));

            return dataTable;
        }

        /// <summary>
        /// Event handler for process exit
        /// </summary>
        private void OnProcessExit(object? sender, EventArgs e)
        {
            Dispose();
        }

        /// <summary>
        /// Event handler for cancel key press
        /// </summary>
        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            Dispose();
        }

        /// <summary>
        /// Disposes the database connection gracefully
        /// </summary>
        public void Dispose()
        {
            lock (_lockObject)
            {
                if (!_disposed)
                {
                    if (_connection?.State == ConnectionState.Open)
                    {
                        try
                        {
                            // Checkpoint WAL file
                            using (var command = new SQLiteCommand("PRAGMA wal_checkpoint(TRUNCATE);", _connection))
                            {
                                command.ExecuteNonQuery();
                            }
                        }
                        catch
                        {
                            // Ignore checkpoint errors during shutdown
                        }
                    }

                    _connection?.Close();
                    _connection?.Dispose();
                    _disposed = true;

                    // Unregister event handlers
                    AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                    Console.CancelKeyPress -= OnCancelKeyPress;
                }
            }
        }
    }
}