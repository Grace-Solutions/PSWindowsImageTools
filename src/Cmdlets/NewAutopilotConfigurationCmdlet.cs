using System;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Creates a new Windows Autopilot configuration template
    /// </summary>
    [Cmdlet(VerbsCommon.New, "AutopilotConfiguration")]
    [OutputType(typeof(AutopilotConfiguration))]
    public class NewAutopilotConfigurationCmdlet : PSCmdlet
    {
        /// <summary>
        /// Azure AD tenant ID
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Azure AD tenant domain
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public string TenantDomain { get; set; } = string.Empty;

        /// <summary>
        /// Device name template (e.g., "XYL-%SERIAL%")
        /// </summary>
        [Parameter]
        public string DeviceName { get; set; } = "%SERIAL%";

        /// <summary>
        /// Comment for the profile
        /// </summary>
        [Parameter]
        public string Comment { get; set; } = "Profile Standard Autopilot Deployment Profile";

        private AutopilotService _autopilotService = new AutopilotService();

        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", 
                    $"Creating new Autopilot configuration for tenant: {TenantDomain}");

                var configuration = _autopilotService.CreateDefaultConfiguration(TenantId, TenantDomain, this);

                // Apply optional parameters
                if (!string.IsNullOrEmpty(DeviceName) && DeviceName != "%SERIAL%")
                {
                    configuration.CloudAssignedDeviceName = DeviceName;
                }

                if (!string.IsNullOrEmpty(Comment) && Comment != "Profile Standard Autopilot Deployment Profile")
                {
                    configuration.CommentFile = Comment;
                }

                WriteObject(configuration);

                LoggingService.WriteVerbose(this, "General", "Autopilot configuration template created successfully");
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "CreationError", ErrorCategory.NotSpecified, TenantId));
            }
        }

        protected override void EndProcessing()
        {
            _autopilotService = null!;
        }
    }
}
