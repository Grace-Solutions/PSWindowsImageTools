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
                int buildsCount = 0, updatesCount = 0, downloadsCount = 0, eventsCount = 0;
                
                try
                {
                    using (var dbService = ConfigurationService.GetDatabaseService())
                    {
                        if (dbService != null)
                        {
                            // Get current record counts
                            var buildsTable = dbService.ExecuteQuery("SELECT COUNT(*) as Count FROM Builds");
                            var updatesTable = dbService.ExecuteQuery("SELECT COUNT(*) as Count FROM Updates");
                            var downloadsTable = dbService.ExecuteQuery("SELECT COUNT(*) as Count FROM Downloads");
                            var eventsTable = dbService.ExecuteQuery("SELECT COUNT(*) as Count FROM BuildProcessingEvents");

                            if (buildsTable.Rows.Count > 0) buildsCount = Convert.ToInt32(buildsTable.Rows[0]["Count"]);
                            if (updatesTable.Rows.Count > 0) updatesCount = Convert.ToInt32(updatesTable.Rows[0]["Count"]);
                            if (downloadsTable.Rows.Count > 0) downloadsCount = Convert.ToInt32(downloadsTable.Rows[0]["Count"]);
                            if (eventsTable.Rows.Count > 0) eventsCount = Convert.ToInt32(eventsTable.Rows[0]["Count"]);

                            LoggingService.WriteVerbose(this, $"Current record counts - Builds: {buildsCount}, Updates: {updatesCount}, Downloads: {downloadsCount}, Events: {eventsCount}");
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
                LoggingService.LogOperationComplete(this, "ClearDatabase", duration, 
                    $"Cleared {buildsCount + updatesCount + downloadsCount + eventsCount} total records");

                // Report results
                WriteObject("Database cleared successfully.");
                WriteObject($"Records removed:");
                WriteObject($"  Builds: {buildsCount}");
                WriteObject($"  Updates: {updatesCount}");
                WriteObject($"  Downloads: {downloadsCount}");
                WriteObject($"  Processing Events: {eventsCount}");
                WriteObject($"  Total: {buildsCount + updatesCount + downloadsCount + eventsCount}");
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
