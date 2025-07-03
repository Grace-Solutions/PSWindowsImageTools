using System;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Uninstalls Windows ADK and WinPE add-on silently
    /// </summary>
    [Cmdlet(VerbsLifecycle.Uninstall, "ADK", SupportsShouldProcess = true)]
    [OutputType(typeof(bool))]
    public class UninstallADKCmdlet : PSCmdlet
    {
        private const string ComponentName = "Uninstall-ADK";

        /// <summary>
        /// Remove all ADK installations found on the system
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Remove all ADK installations found on the system")]
        public SwitchParameter All { get; set; }

        /// <summary>
        /// Force uninstallation without confirmation prompts
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Force uninstallation without confirmation prompts")]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName, "Uninstall Windows ADK");

                LoggingService.WriteVerbose(this, ComponentName, "Starting ADK uninstallation process");

                // Detect existing installations
                var adkService = new ADKService();
                var installations = adkService.DetectADKInstallations(this);

                if (installations.Count == 0)
                {
                    LoggingService.WriteVerbose(this, ComponentName, "No ADK installations found");
                    WriteWarning("No Windows ADK installations found on this system.");
                    WriteObject(true);
                    return;
                }

                // Determine what to uninstall
                var installationsToRemove = All.IsPresent 
                    ? installations 
                    : new[] { installations.OrderByDescending(adk => adk.Version).First() }.ToList();

                LoggingService.WriteVerbose(this, ComponentName, 
                    $"Found {installations.Count} ADK installations, will remove {installationsToRemove.Count}");

                // Show what will be uninstalled
                foreach (var installation in installationsToRemove)
                {
                    LoggingService.WriteVerbose(this, ComponentName, 
                        $"Will uninstall: {installation.DisplayName} v{installation.Version} from {installation.InstallationPath?.FullName}");
                }

                // Confirm uninstallation if not forced
                if (!Force.IsPresent)
                {
                    var installationList = string.Join(", ", installationsToRemove.Select(adk => $"{adk.DisplayName} v{adk.Version}"));
                    var confirmationMessage = All.IsPresent
                        ? $"Remove all ADK installations: {installationList}?"
                        : $"Remove latest ADK installation: {installationList}?";

                    if (!ShouldProcess("Windows ADK", confirmationMessage))
                    {
                        LoggingService.WriteVerbose(this, ComponentName, "Uninstallation cancelled by user");
                        WriteObject(false);
                        return;
                    }
                }

                // Perform uninstallation
                LoggingService.WriteVerbose(this, ComponentName, "Starting uninstallation process");

                var managementService = new ADKManagementService();
                var success = managementService.UninstallADK(All.IsPresent, this);

                if (success)
                {
                    LoggingService.WriteVerbose(this, ComponentName, "ADK uninstallation completed successfully");
                    
                    // Verify uninstallation
                    var remainingInstallations = adkService.DetectADKInstallations(this);
                    var expectedRemaining = All.IsPresent ? 0 : installations.Count - 1;
                    
                    if (remainingInstallations.Count == expectedRemaining)
                    {
                        WriteInformation(new InformationRecord(
                            "✓ Windows ADK uninstallation completed successfully",
                            "UninstallationSuccess"));
                    }
                    else
                    {
                        WriteWarning($"Uninstallation may not have completed fully. Expected {expectedRemaining} remaining installations, found {remainingInstallations.Count}");
                    }
                }
                else
                {
                    WriteError(new ErrorRecord(
                        new InvalidOperationException("ADK uninstallation failed"),
                        "UninstallationFailed",
                        ErrorCategory.OperationStopped,
                        null));
                }

                WriteObject(success);
                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "ADK uninstallation", operationStartTime, "ADK uninstallation completed");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, "Failed to uninstall Windows ADK", ex);
                WriteError(new ErrorRecord(ex, "ADKUninstallationError", ErrorCategory.NotSpecified, null));
            }
        }

        /// <summary>
        /// Validates parameters and permissions before processing
        /// </summary>
        protected override void BeginProcessing()
        {
            // Check for administrator privileges
            var isAdmin = new System.Security.Principal.WindowsPrincipal(
                System.Security.Principal.WindowsIdentity.GetCurrent())
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

            if (!isAdmin)
            {
                WriteError(new ErrorRecord(
                    new UnauthorizedAccessException("Administrator privileges are required to uninstall Windows ADK"),
                    "InsufficientPrivileges",
                    ErrorCategory.PermissionDenied,
                    null));
                return;
            }

            LoggingService.WriteVerbose(this, ComponentName, "Permission validation completed");
        }

        /// <summary>
        /// Provides cleanup and final status
        /// </summary>
        protected override void EndProcessing()
        {
            LoggingService.WriteVerbose(this, ComponentName, "ADK uninstallation process completed");
        }
    }
}
