using System;
using System.IO;
using System.Management.Automation;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Cmdlet for configuring the Windows Image database settings
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "WindowsImageDatabaseConfiguration")]
    [OutputType(typeof(void))]
    public class SetWindowsImageDatabaseConfigurationCmdlet : PSCmdlet
    {
        /// <summary>
        /// Path to the SQLite database file
        /// </summary>
        [Parameter(
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Path to the SQLite database file")]
        [ValidateNotNullOrEmpty]
        public FileInfo? Path { get; set; }

        /// <summary>
        /// Disables database usage for the current session
        /// </summary>
        [Parameter(
            HelpMessage = "Disables database usage for the current session")]
        public SwitchParameter Disable { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.LogOperationStart(this, "SetDatabaseConfiguration");

                if (Disable.IsPresent)
                {
                    // Disable database usage
                    ConfigurationService.IsDatabaseDisabled = true;
                    LoggingService.WriteVerbose(this, "Database usage disabled for current session");
                    WriteObject("Database usage has been disabled for the current session.");
                    return;
                }

                if (Path != null)
                {
                    // Validate the provided path
                    var expandedPath = ConfigurationService.ExpandEnvironmentVariables(Path.FullName);
                    
                    LoggingService.WriteVerbose(this, $"Validating database path: {expandedPath}");
                    
                    if (!ConfigurationService.ValidateDatabasePath(expandedPath))
                    {
                        var errorMessage = $"Cannot access or create database at path: {expandedPath}";
                        LoggingService.WriteError(this, "SetDatabaseConfiguration", errorMessage);
                        ThrowTerminatingError(new ErrorRecord(
                            new InvalidOperationException(errorMessage),
                            "InvalidDatabasePath",
                            ErrorCategory.InvalidArgument,
                            expandedPath));
                        return;
                    }

                    // Set the database path
                    ConfigurationService.DatabasePath = expandedPath;
                    ConfigurationService.IsDatabaseDisabled = false;
                    
                    LoggingService.WriteVerbose(this, $"Database path set to: {expandedPath}");
                    WriteObject($"Database path has been set to: {expandedPath}");

                    // Test database connectivity by initializing it
                    try
                    {
                        using (var dbService = ConfigurationService.GetDatabaseService())
                        {
                            if (dbService != null)
                            {
                                dbService.InitializeDatabase();
                                LoggingService.WriteVerbose(this, "Database connectivity verified");
                                WriteObject("Database connectivity verified successfully.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteWarning(this, "SetDatabaseConfiguration", 
                            $"Database path set but connectivity test failed: {ex.Message}");
                        WriteWarning($"Database path set but connectivity test failed: {ex.Message}");
                    }
                }
                else
                {
                    // Show current configuration
                    var moduleInfo = ConfigurationService.GetModuleInfo();
                    
                    WriteObject("Current Windows Image Database Configuration:");
                    WriteObject($"  Database Path: {moduleInfo.DatabasePath}");
                    WriteObject($"  Database Disabled: {moduleInfo.IsDatabaseDisabled}");
                    WriteObject($"  Default Mount Root: {moduleInfo.DefaultMountRootDirectory}");
                    
                    if (!moduleInfo.IsDatabaseDisabled)
                    {
                        // Test current database connectivity
                        try
                        {
                            using (var dbService = ConfigurationService.GetDatabaseService())
                            {
                                if (dbService != null)
                                {
                                    dbService.InitializeDatabase();
                                    WriteObject("  Database Status: Connected");
                                }
                                else
                                {
                                    WriteObject("  Database Status: Disabled");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteObject($"  Database Status: Error - {ex.Message}");
                        }
                    }
                    else
                    {
                        WriteObject("  Database Status: Disabled");
                    }
                }

                LoggingService.LogOperationComplete(this, "SetDatabaseConfiguration", TimeSpan.Zero);
            }
            catch (Exception ex)
            {
                LoggingService.LogOperationFailure(this, "SetDatabaseConfiguration", ex);
                ThrowTerminatingError(new ErrorRecord(
                    ex,
                    "SetDatabaseConfigurationFailed",
                    ErrorCategory.NotSpecified,
                    null));
            }
        }
    }
}
