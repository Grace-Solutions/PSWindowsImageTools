using System;
using System.Management.Automation;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Cmdlet for explicitly initializing the Windows Image database
    /// </summary>
    [Cmdlet(VerbsCommon.New, "WindowsImageDatabase")]
    [OutputType(typeof(string))]
    public class NewWindowsImageDatabaseCmdlet : PSCmdlet
    {
        /// <summary>
        /// Forces recreation of the database even if it already exists
        /// </summary>
        [Parameter(
            HelpMessage = "Forces recreation of the database even if it already exists")]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.LogOperationStart(this, "NewDatabase");

                // Check if database is disabled
                if (ConfigurationService.IsDatabaseDisabled)
                {
                    var errorMessage = "Database usage is currently disabled. Use Set-WindowsImageDatabaseConfiguration to enable it.";
                    LoggingService.WriteError(this, "NewDatabase", errorMessage);
                    ThrowTerminatingError(new ErrorRecord(
                        new InvalidOperationException(errorMessage),
                        "DatabaseDisabled",
                        ErrorCategory.InvalidOperation,
                        null));
                    return;
                }

                var databasePath = ConfigurationService.DatabasePath;
                LoggingService.WriteVerbose(this, $"Initializing database at: {databasePath}");

                // Check if database already exists
                if (System.IO.File.Exists(databasePath) && !Force.IsPresent)
                {
                    LoggingService.WriteVerbose(this, "Database already exists, skipping initialization");
                    WriteObject($"Database already exists at: {databasePath}");
                    WriteObject("Use -Force parameter to recreate the database.");
                    return;
                }

                // Delete existing database if Force is specified
                if (System.IO.File.Exists(databasePath) && Force.IsPresent)
                {
                    LoggingService.WriteVerbose(this, "Force parameter specified, deleting existing database");
                    try
                    {
                        System.IO.File.Delete(databasePath);
                        LoggingService.WriteVerbose(this, "Existing database deleted successfully");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteError(this, "NewDatabase", $"Failed to delete existing database: {ex.Message}", ex);
                        ThrowTerminatingError(new ErrorRecord(
                            ex,
                            "DeleteDatabaseFailed",
                            ErrorCategory.WriteError,
                            databasePath));
                        return;
                    }
                }

                // Initialize the database
                var startTime = DateTime.UtcNow;
                
                using (var dbService = ConfigurationService.GetDatabaseService())
                {
                    if (dbService == null)
                    {
                        var errorMessage = "Failed to create database service instance";
                        LoggingService.WriteError(this, "NewDatabase", errorMessage);
                        ThrowTerminatingError(new ErrorRecord(
                            new InvalidOperationException(errorMessage),
                            "DatabaseServiceCreationFailed",
                            ErrorCategory.NotSpecified,
                            null));
                        return;
                    }

                    LoggingService.WriteVerbose(this, "Creating database schema...");
                    dbService.InitializeDatabase();
                    LoggingService.WriteVerbose(this, "Database schema created successfully");
                }

                var duration = DateTime.UtcNow - startTime;
                LoggingService.LogOperationComplete(this, "NewDatabase", duration, $"Database created at: {databasePath}");

                // Verify database was created successfully
                if (System.IO.File.Exists(databasePath))
                {
                    var fileInfo = new System.IO.FileInfo(databasePath);
                    WriteObject($"Database successfully created at: {databasePath}");
                    WriteObject($"Database file size: {fileInfo.Length} bytes");
                    WriteObject($"Creation time: {fileInfo.CreationTimeUtc:yyyy-MM-ddTHH:mm:ss.fffZ} UTC");
                }
                else
                {
                    var errorMessage = "Database creation appeared to succeed but file was not found";
                    LoggingService.WriteError(this, "NewDatabase", errorMessage);
                    ThrowTerminatingError(new ErrorRecord(
                        new InvalidOperationException(errorMessage),
                        "DatabaseVerificationFailed",
                        ErrorCategory.NotSpecified,
                        databasePath));
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogOperationFailure(this, "NewDatabase", ex);
                ThrowTerminatingError(new ErrorRecord(
                    ex,
                    "NewDatabaseFailed",
                    ErrorCategory.NotSpecified,
                    null));
            }
        }
    }
}
