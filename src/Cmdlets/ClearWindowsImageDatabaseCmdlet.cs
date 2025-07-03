using System;
using System.Management.Automation;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Cmdlet for clearing all data from the Windows Image database
    /// </summary>
    [Cmdlet(VerbsCommon.Clear, "WindowsImageDatabase", 
        SupportsShouldProcess = true, 
        ConfirmImpact = ConfirmImpact.High)]
    [OutputType(typeof(string))]
    public class ClearWindowsImageDatabaseCmdlet : PSCmdlet
    {
        /// <summary>
        /// Forces the operation without confirmation
        /// </summary>
        [Parameter(
            HelpMessage = "Forces the operation without confirmation")]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.LogOperationStart(this, "ClearDatabase");

                // Check if database is disabled
                if (ConfigurationService.IsDatabaseDisabled)
                {
                    var errorMessage = "Database usage is currently disabled. Use Set-WindowsImageDatabaseConfiguration to enable it.";
                    LoggingService.WriteError(this, "ClearDatabase", errorMessage);
                    ThrowTerminatingError(new ErrorRecord(
                        new InvalidOperationException(errorMessage),
                        "DatabaseDisabled",
                        ErrorCategory.InvalidOperation,
                        null));
                    return;
                }

                var databasePath = ConfigurationService.DatabasePath;
                LoggingService.WriteVerbose(this, $"Preparing to clear database at: {databasePath}");

                // Check if database exists
                if (!System.IO.File.Exists(databasePath))
                {
                    LoggingService.WriteVerbose(this, "Database file does not exist, nothing to clear");
                    WriteObject($"Database file does not exist at: {databasePath}");
                    WriteObject("Nothing to clear.");
                    return;
                }

                // Confirm the operation unless Force is specified
                var confirmationMessage = $"This will permanently delete all data from the Windows Image database at: {databasePath}";
                var confirmationCaption = "Clear Windows Image Database";
                
                if (!Force.IsPresent && !ShouldProcess(confirmationMessage, confirmationCaption))
                {
                    LoggingService.WriteVerbose(this, "Operation cancelled by user");
                    WriteObject("Operation cancelled.");
                    return;
                }

                // Get record counts before clearing
                int buildsCount = 0, updatesCount = 0, imagesCount = 0, operationsCount = 0, inventoryCount = 0;
                
                try
                {
                    using (var dbService = ConfigurationService.GetDatabaseService())
                    {
                        if (dbService != null)
                        {
                            // Get current record counts
                            var buildsTable = dbService.ExecuteQuery("SELECT COUNT(*) as Count FROM Builds");
                            var updatesTable = dbService.ExecuteQuery("SELECT COUNT(*) as Count FROM Updates");
                            var imagesTable = dbService.ExecuteQuery("SELECT COUNT(*) as Count FROM Images");
                            var operationsTable = dbService.ExecuteQuery("SELECT COUNT(*) as Count FROM Operations");
                            var inventoryTable = dbService.ExecuteQuery("SELECT COUNT(*) as Count FROM Inventory");

                            if (buildsTable.Rows.Count > 0) buildsCount = Convert.ToInt32(buildsTable.Rows[0]["Count"]);
                            if (updatesTable.Rows.Count > 0) updatesCount = Convert.ToInt32(updatesTable.Rows[0]["Count"]);
                            if (imagesTable.Rows.Count > 0) imagesCount = Convert.ToInt32(imagesTable.Rows[0]["Count"]);
                            if (operationsTable.Rows.Count > 0) operationsCount = Convert.ToInt32(operationsTable.Rows[0]["Count"]);
                            if (inventoryTable.Rows.Count > 0) inventoryCount = Convert.ToInt32(inventoryTable.Rows[0]["Count"]);

                            LoggingService.WriteVerbose(this, $"Current record counts - Builds: {buildsCount}, Updates: {updatesCount}, Images: {imagesCount}, Operations: {operationsCount}, Inventory: {inventoryCount}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.WriteWarning(this, "ClearDatabase", $"Could not retrieve record counts: {ex.Message}");
                }

                // Clear the database
                var startTime = DateTime.UtcNow;
                
                using (var dbService = ConfigurationService.GetDatabaseService())
                {
                    if (dbService == null)
                    {
                        var errorMessage = "Failed to create database service instance";
                        LoggingService.WriteError(this, "ClearDatabase", errorMessage);
                        ThrowTerminatingError(new ErrorRecord(
                            new InvalidOperationException(errorMessage),
                            "DatabaseServiceCreationFailed",
                            ErrorCategory.NotSpecified,
                            null));
                        return;
                    }

                    LoggingService.WriteVerbose(this, "Clearing all database tables...");
                    dbService.ClearAllData();
                    LoggingService.WriteVerbose(this, "Database tables cleared successfully");
                }

                var duration = DateTime.UtcNow - startTime;
                var totalRecords = buildsCount + updatesCount + imagesCount + operationsCount + inventoryCount;
                LoggingService.LogOperationComplete(this, "ClearDatabase", duration,
                    $"Cleared {totalRecords} total records");

                // Report results
                WriteObject("Database cleared successfully.");
                WriteObject($"Records removed:");
                WriteObject($"  Builds: {buildsCount}");
                WriteObject($"  Updates: {updatesCount}");
                WriteObject($"  Images: {imagesCount}");
                WriteObject($"  Operations: {operationsCount}");
                WriteObject($"  Inventory: {inventoryCount}");
                WriteObject($"  Total: {totalRecords}");
                WriteObject($"Operation completed in {duration.TotalMilliseconds:F0}ms");
            }
            catch (Exception ex)
            {
                LoggingService.LogOperationFailure(this, "ClearDatabase", ex);
                ThrowTerminatingError(new ErrorRecord(
                    ex,
                    "ClearDatabaseFailed",
                    ErrorCategory.NotSpecified,
                    null));
            }
        }
    }
}
