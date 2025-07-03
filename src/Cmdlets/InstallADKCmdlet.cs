using System;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Downloads and installs the latest Windows ADK silently
    /// </summary>
    [Cmdlet(VerbsLifecycle.Install, "ADK")]
    [OutputType(typeof(ADKInfo))]
    public class InstallADKCmdlet : PSCmdlet
    {
        private const string ComponentName = "Install-ADK";

        /// <summary>
        /// Custom installation path for ADK
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Custom installation path for ADK")]
        [ValidateNotNullOrEmpty]
        public string? InstallPath { get; set; }

        /// <summary>
        /// Include WinPE add-on in the installation
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Include WinPE add-on in the installation")]
        public SwitchParameter IncludeWinPE { get; set; } = true;

        /// <summary>
        /// Include Deployment Tools in the installation
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Include Deployment Tools in the installation")]
        public SwitchParameter IncludeDeploymentTools { get; set; } = true;

        /// <summary>
        /// Force installation even if ADK is already installed
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Force installation even if ADK is already installed")]
        public SwitchParameter Force { get; set; }



        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName, "Install Windows ADK");

                LoggingService.WriteVerbose(this, ComponentName, "Starting ADK installation process");

                // Check for existing installations if not forcing
                if (!Force.IsPresent)
                {
                    var adkService = new ADKService();
                    var existingInstallations = adkService.DetectADKInstallations(this);

                    if (existingInstallations.Count > 0)
                    {
                        var latest = existingInstallations.OrderByDescending(adk => adk.Version).First();
                        
                        LoggingService.WriteVerbose(this, ComponentName, 
                            $"Found existing ADK installation: {latest.DisplayName} v{latest.Version}");

                        // Check if existing installation meets requirements
                        var meetsRequirements = (!IncludeWinPE.IsPresent || latest.HasWinPEAddon) &&
                                              (!IncludeDeploymentTools.IsPresent || latest.HasDeploymentTools);

                        if (meetsRequirements)
                        {
                            LoggingService.WriteVerbose(this, ComponentName,
                                "Existing ADK meets requirements, skipping installation");
                            WriteObject(latest);
                            return;
                        }
                        else
                        {
                            LoggingService.WriteVerbose(this, ComponentName, 
                                "Existing ADK does not meet requirements, proceeding with installation");
                        }
                    }
                }

                // Validate installation path if provided
                if (!string.IsNullOrEmpty(InstallPath))
                {
                    try
                    {
                        var parentDir = System.IO.Path.GetDirectoryName(InstallPath);
                        if (!string.IsNullOrEmpty(parentDir) && !System.IO.Directory.Exists(parentDir))
                        {
                            LoggingService.WriteVerbose(this, ComponentName, 
                                $"Creating installation directory: {parentDir}");
                            System.IO.Directory.CreateDirectory(parentDir);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteError(new ErrorRecord(
                            new ArgumentException($"Invalid installation path: {InstallPath}. {ex.Message}"),
                            "InvalidInstallPath",
                            ErrorCategory.InvalidArgument,
                            InstallPath));
                        return;
                    }
                }

                // Perform installation
                LoggingService.WriteVerbose(this, ComponentName, 
                    $"Installing ADK with WinPE: {IncludeWinPE.IsPresent}, Deployment Tools: {IncludeDeploymentTools.IsPresent}");

                var managementService = new ADKManagementService();
                var installedADK = managementService.InstallLatestADK(
                    InstallPath,
                    IncludeWinPE.IsPresent,
                    IncludeDeploymentTools.IsPresent,
                    this);

                if (installedADK != null)
                {
                    LoggingService.WriteVerbose(this, ComponentName, 
                        $"Successfully installed ADK: {installedADK.DisplayName} v{installedADK.Version}");
                    
                    WriteObject(installedADK);
                }
                else
                {
                    WriteError(new ErrorRecord(
                        new InvalidOperationException("ADK installation failed - no installation detected after completion"),
                        "InstallationFailed",
                        ErrorCategory.OperationStopped,
                        null));
                }

                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "ADK installation", operationStartTime, "ADK installation completed");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, "Failed to install Windows ADK", ex);
                WriteError(new ErrorRecord(ex, "ADKInstallationError", ErrorCategory.NotSpecified, null));
            }
        }

        /// <summary>
        /// Validates parameters before processing
        /// </summary>
        protected override void BeginProcessing()
        {
            // Validate that at least one component is selected
            if (!IncludeWinPE.IsPresent && !IncludeDeploymentTools.IsPresent)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("At least one component must be selected: -IncludeWinPE or -IncludeDeploymentTools"),
                    "NoComponentsSelected",
                    ErrorCategory.InvalidArgument,
                    null));
                return;
            }

            // Check for administrator privileges
            var isAdmin = new System.Security.Principal.WindowsPrincipal(
                System.Security.Principal.WindowsIdentity.GetCurrent())
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

            if (!isAdmin)
            {
                WriteError(new ErrorRecord(
                    new UnauthorizedAccessException("Administrator privileges are required to install Windows ADK"),
                    "InsufficientPrivileges",
                    ErrorCategory.PermissionDenied,
                    null));
                return;
            }

            LoggingService.WriteVerbose(this, ComponentName, "Parameter validation completed");
        }
    }
}
