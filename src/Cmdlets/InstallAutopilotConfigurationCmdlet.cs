using System;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Installs Windows Autopilot configuration to mounted Windows images
    /// </summary>
    [Cmdlet(VerbsLifecycle.Install, "AutopilotConfiguration")]
    [OutputType(typeof(AutopilotApplicationResult))]
    public class InstallAutopilotConfigurationCmdlet : PSCmdlet
    {
        /// <summary>
        /// Mounted Windows images to apply configuration to
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [ValidateNotNull]
        public MountedWindowsImage[] MountedImages { get; set; } = Array.Empty<MountedWindowsImage>();

        /// <summary>
        /// Autopilot configuration to apply
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNull]
        public AutopilotConfiguration Configuration { get; set; } = new AutopilotConfiguration();

        /// <summary>
        /// Show what would happen without actually applying
        /// </summary>
        [Parameter]
        public SwitchParameter WhatIf { get; set; }

        /// <summary>
        /// Continue processing even if some images fail
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        private AutopilotService _autopilotService = new AutopilotService();

        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", 
                    $"Applying Autopilot configuration to {MountedImages.Length} mounted images");

                if (WhatIf.IsPresent)
                {
                    ProcessWhatIf();
                    return;
                }

                // Validate configuration before applying
                var validationErrors = Configuration.Validate();
                if (validationErrors.Count > 0)
                {
                    var errorMessage = $"Invalid Autopilot configuration: {string.Join(", ", validationErrors)}";
                    
                    if (Force.IsPresent)
                    {
                        WriteWarning(errorMessage + " - Continuing due to -Force parameter");
                    }
                    else
                    {
                        WriteError(new ErrorRecord(
                            new InvalidOperationException(errorMessage),
                            "InvalidConfiguration",
                            ErrorCategory.InvalidData,
                            Configuration));
                        return;
                    }
                }

                // Apply configuration to all mounted images
                var results = _autopilotService.ApplyConfiguration(MountedImages, Configuration, this);

                // Output results
                foreach (var result in results)
                {
                    WriteObject(result);

                    if (!result.Success && !Force.IsPresent)
                    {
                        WriteError(new ErrorRecord(
                            new InvalidOperationException(result.ErrorMessage),
                            "ApplicationFailed",
                            ErrorCategory.NotSpecified,
                            result.MountedImage));
                    }
                }

                var successCount = 0;
                var failureCount = 0;
                foreach (var result in results)
                {
                    if (result.Success) successCount++;
                    else failureCount++;
                }

                LoggingService.WriteVerbose(this, "General", 
                    $"Autopilot configuration application completed: {successCount} succeeded, {failureCount} failed");
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "ApplicationError", ErrorCategory.NotSpecified, MountedImages));
            }
        }

        private void ProcessWhatIf()
        {
            WriteObject("What if: Performing the operation \"Install Autopilot Configuration\" on the following targets:");
            
            foreach (var image in MountedImages)
            {
                if (image.MountPath == null)
                    throw new InvalidOperationException("Mount path is null");
                var autopilotPath = System.IO.Path.Combine(image.MountPath.FullName, "Windows", "Provisioning", "Autopilot", "AutopilotConfigurationFile.json");
                WriteObject($"  Target: {image.ImageName} ({image.MountPath.FullName})");
                WriteObject($"  Configuration would be written to: {autopilotPath}");
                WriteObject($"  Tenant: {Configuration.CloudAssignedTenantDomain}");
                WriteObject($"  Forced Enrollment: {(Configuration.CloudAssignedForcedEnrollment == 1 ? "Enabled" : "Disabled")}");
                WriteObject("");
            }
        }

        protected override void EndProcessing()
        {
            _autopilotService = null!;
        }
    }
}
