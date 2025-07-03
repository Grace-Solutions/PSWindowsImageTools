using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Loads and parses Windows Autopilot configuration files
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "AutopilotConfiguration")]
    [OutputType(typeof(AutopilotConfiguration))]
    public class GetAutopilotConfigurationCmdlet : PSCmdlet
    {
        /// <summary>
        /// Autopilot JSON file to load
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        public FileInfo File { get; set; } = null!;

        /// <summary>
        /// Validate the configuration after loading
        /// </summary>
        [Parameter]
        public SwitchParameter Validate { get; set; }

        private AutopilotService _autopilotService = new AutopilotService();

        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", "Starting Autopilot configuration loading");

                if (!File.Exists)
                {
                    WriteError(new ErrorRecord(
                        new FileNotFoundException($"File not found: {File.FullName}"),
                        "FileNotFound",
                        ErrorCategory.ObjectNotFound,
                        File));
                    return;
                }

                ProcessFile(File.FullName);

                LoggingService.WriteVerbose(this, "General", "Autopilot configuration loading completed");
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "AutopilotConfigurationError", ErrorCategory.NotSpecified, File));
            }
        }



        private void ProcessFile(string filePath)
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", $"Loading Autopilot configuration from file: {filePath}");

                var configuration = _autopilotService.LoadConfiguration(filePath, this);

                if (Validate.IsPresent)
                {
                    var validationErrors = configuration.Validate();
                    if (validationErrors.Any())
                    {
                        WriteWarning($"Validation errors in {filePath}: {string.Join(", ", validationErrors)}");
                    }
                    else
                    {
                        LoggingService.WriteVerbose(this, "General", $"Configuration validation passed for {filePath}");
                    }
                }

                WriteObject(configuration);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "FileProcessingError", ErrorCategory.NotSpecified, filePath));
            }
        }



        protected override void EndProcessing()
        {
            _autopilotService = null!;
        }
    }

    /// <summary>
    /// Modifies Windows Autopilot configuration properties
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "AutopilotConfiguration")]
    [OutputType(typeof(AutopilotConfiguration))]
    public class SetAutopilotConfigurationCmdlet : PSCmdlet
    {
        /// <summary>
        /// Autopilot configuration to modify
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [ValidateNotNull]
        public AutopilotConfiguration Configuration { get; set; } = new AutopilotConfiguration();

        /// <summary>
        /// Azure AD tenant ID
        /// </summary>
        [Parameter]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Azure AD tenant domain
        /// </summary>
        [Parameter]
        public string TenantDomain { get; set; } = string.Empty;

        /// <summary>
        /// Device name template (e.g., "XYL-%SERIAL%")
        /// </summary>
        [Parameter]
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// OOBE configuration flags
        /// </summary>
        [Parameter]
        public int OobeConfig { get; set; } = -1;

        /// <summary>
        /// Domain join method (0 = Azure AD, 1 = Hybrid Azure AD)
        /// </summary>
        [Parameter]
        [ValidateRange(0, 1)]
        public int DomainJoinMethod { get; set; } = -1;

        /// <summary>
        /// Disable Autopilot updates
        /// </summary>
        [Parameter]
        public SwitchParameter DisableAutopilotUpdate { get; set; }

        /// <summary>
        /// Enable Autopilot updates
        /// </summary>
        [Parameter]
        public SwitchParameter EnableAutopilotUpdate { get; set; }

        /// <summary>
        /// Autopilot update timeout in milliseconds
        /// </summary>
        [Parameter]
        [ValidateRange(0, int.MaxValue)]
        public int UpdateTimeout { get; set; } = -1;

        /// <summary>
        /// Force enrollment setting (0 or 1)
        /// </summary>
        [Parameter]
        [ValidateRange(0, 1)]
        public int ForcedEnrollment { get; set; } = -1;

        /// <summary>
        /// Return the modified configuration
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", "Modifying Autopilot configuration");

                // Create a copy to avoid modifying the original
                var modifiedConfig = Configuration.Clone();

                // Apply modifications
                if (!string.IsNullOrEmpty(TenantId))
                {
                    modifiedConfig.CloudAssignedTenantId = TenantId;
                    modifiedConfig.IsModified = true;
                }

                if (!string.IsNullOrEmpty(TenantDomain))
                {
                    modifiedConfig.CloudAssignedTenantDomain = TenantDomain;
                    modifiedConfig.IsModified = true;
                }

                if (!string.IsNullOrEmpty(DeviceName))
                {
                    modifiedConfig.CloudAssignedDeviceName = DeviceName;
                    modifiedConfig.IsModified = true;
                }

                if (OobeConfig >= 0)
                {
                    modifiedConfig.CloudAssignedOobeConfig = OobeConfig;
                    modifiedConfig.IsModified = true;
                }

                if (DomainJoinMethod >= 0)
                {
                    modifiedConfig.CloudAssignedDomainJoinMethod = DomainJoinMethod;
                    modifiedConfig.IsModified = true;
                }

                if (DisableAutopilotUpdate.IsPresent)
                {
                    modifiedConfig.CloudAssignedAutopilotUpdateDisabled = 1;
                    modifiedConfig.IsModified = true;
                }

                if (EnableAutopilotUpdate.IsPresent)
                {
                    modifiedConfig.CloudAssignedAutopilotUpdateDisabled = 0;
                    modifiedConfig.IsModified = true;
                }

                if (UpdateTimeout >= 0)
                {
                    modifiedConfig.CloudAssignedAutopilotUpdateTimeout = UpdateTimeout;
                    modifiedConfig.IsModified = true;
                }

                if (ForcedEnrollment >= 0)
                {
                    modifiedConfig.CloudAssignedForcedEnrollment = ForcedEnrollment;
                    modifiedConfig.IsModified = true;
                }

                LoggingService.WriteVerbose(this, "General", "Autopilot configuration modification completed");

                if (PassThru.IsPresent)
                {
                    WriteObject(modifiedConfig);
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "ConfigurationModificationError", ErrorCategory.NotSpecified, Configuration));
            }
        }
    }
}
