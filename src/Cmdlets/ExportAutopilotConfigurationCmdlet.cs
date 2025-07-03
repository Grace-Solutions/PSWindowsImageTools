using System;
using System.IO;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Exports Windows Autopilot configuration to JSON file
    /// </summary>
    [Cmdlet(VerbsData.Export, "AutopilotConfiguration")]
    [OutputType(typeof(FileInfo))]
    public class ExportAutopilotConfigurationCmdlet : PSCmdlet
    {
        /// <summary>
        /// Autopilot configuration to export
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [ValidateNotNull]
        public AutopilotConfiguration Configuration { get; set; } = new AutopilotConfiguration();

        /// <summary>
        /// Output file path for the JSON file
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Overwrite existing file
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Return the created file info
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        private AutopilotService _autopilotService = new AutopilotService();

        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", $"Exporting Autopilot configuration to {Path}");

                // Resolve the output path
                var resolvedPath = GetUnresolvedProviderPathFromPSPath(Path);

                // Check if file exists and Force is not specified
                if (File.Exists(resolvedPath) && !Force.IsPresent)
                {
                    WriteError(new ErrorRecord(
                        new InvalidOperationException($"File already exists: {resolvedPath}. Use -Force to overwrite."),
                        "FileExists",
                        ErrorCategory.ResourceExists,
                        resolvedPath));
                    return;
                }

                // Validate configuration before export
                var validationErrors = Configuration.Validate();
                if (validationErrors.Count > 0)
                {
                    WriteWarning($"Configuration has validation errors: {string.Join(", ", validationErrors)}");
                }

                // Export the configuration
                _autopilotService.SaveConfiguration(Configuration, resolvedPath, this);

                LoggingService.WriteVerbose(this, "General", $"Successfully exported Autopilot configuration to {resolvedPath}");

                if (PassThru.IsPresent)
                {
                    WriteObject(new FileInfo(resolvedPath));
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "ExportError", ErrorCategory.NotSpecified, Configuration));
            }
        }

        protected override void EndProcessing()
        {
            _autopilotService = null!;
        }
    }
}
