using System;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Installs Windows Unattend XML configuration to mounted Windows images
    /// </summary>
    [Cmdlet(VerbsLifecycle.Install, "UnattendXMLConfiguration")]
    [OutputType(typeof(UnattendXMLApplicationResult))]
    public class InstallUnattendXMLConfigurationCmdlet : PSCmdlet
    {
        /// <summary>
        /// Mounted Windows images to install configuration to
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [ValidateNotNull]
        public MountedWindowsImage[] MountedImages { get; set; } = Array.Empty<MountedWindowsImage>();

        /// <summary>
        /// Unattend XML configuration to install
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNull]
        public UnattendXMLConfiguration Configuration { get; set; } = new UnattendXMLConfiguration();

        /// <summary>
        /// Show what would happen without actually installing
        /// </summary>
        [Parameter]
        public SwitchParameter WhatIf { get; set; }

        /// <summary>
        /// Continue processing even if some images fail
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Text encoding for the XML file
        /// </summary>
        [Parameter]
        [ValidateSet("UTF8", "UTF16", "UTF32", "ASCII", "Unicode", "BigEndianUnicode")]
        public string Encoding { get; set; } = "UTF8";

        private UnattendXMLService _unattendService = new UnattendXMLService();

        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", 
                    $"Installing Unattend XML configuration to {MountedImages.Length} mounted images");

                if (WhatIf.IsPresent)
                {
                    ProcessWhatIf();
                    return;
                }

                // Validate configuration before installing
                var validationErrors = Configuration.Validate();
                if (validationErrors.Count > 0)
                {
                    var errorMessage = $"Invalid Unattend XML configuration: {string.Join(", ", validationErrors)}";
                    
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

                // Install configuration to all mounted images
                var results = _unattendService.InstallConfiguration(MountedImages, Configuration, this, Encoding);

                // Output results
                foreach (var result in results)
                {
                    WriteObject(result);

                    if (!result.Success && !Force.IsPresent)
                    {
                        WriteError(new ErrorRecord(
                            new InvalidOperationException(result.ErrorMessage),
                            "InstallationFailed",
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
                    $"Unattend XML configuration installation completed: {successCount} succeeded, {failureCount} failed");
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "InstallationError", ErrorCategory.NotSpecified, MountedImages));
            }
        }

        private void ProcessWhatIf()
        {
            WriteObject("What if: Performing the operation \"Install Unattend XML Configuration\" on the following targets:");
            
            foreach (var image in MountedImages)
            {
                if (image.MountPath == null)
                    throw new InvalidOperationException("Mount path is null");
                var unattendPath = System.IO.Path.Combine(image.MountPath.FullName, "Windows", "Panther", "unattend.xml");
                WriteObject($"  Target: {image.ImageName} ({image.MountPath.FullName})");
                WriteObject($"  Configuration would be written to: {unattendPath}");
                WriteObject($"  Configuration passes: {string.Join(", ", Configuration.ConfigurationPasses)}");
                WriteObject($"  Components: {Configuration.Components.Count}");
                WriteObject($"  Encoding: {Encoding}");
                WriteObject("");
            }
        }

        protected override void EndProcessing()
        {
            _unattendService = null!;
        }
    }
}
