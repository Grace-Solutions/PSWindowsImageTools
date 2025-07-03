using System;
using System.IO;
using System.Management.Automation;
using System.Text;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Exports Windows Unattend XML configuration to file with encoding options
    /// </summary>
    [Cmdlet(VerbsData.Export, "UnattendXMLConfiguration")]
    [OutputType(typeof(FileInfo))]
    public class ExportUnattendXMLConfigurationCmdlet : PSCmdlet
    {
        /// <summary>
        /// Unattend XML configuration to export
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [ValidateNotNull]
        public UnattendXMLConfiguration Configuration { get; set; } = new UnattendXMLConfiguration();

        /// <summary>
        /// Output file path for the XML file
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Text encoding for the XML file
        /// </summary>
        [Parameter]
        [ValidateSet("UTF8", "UTF16", "UTF32", "ASCII", "Unicode", "BigEndianUnicode")]
        public string Encoding { get; set; } = "UTF8";

        /// <summary>
        /// Overwrite existing file
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// Format the XML with indentation
        /// </summary>
        [Parameter]
        public SwitchParameter Indent { get; set; } = true;

        /// <summary>
        /// Custom indentation characters (default: two spaces)
        /// </summary>
        [Parameter]
        public string IndentChars { get; set; } = "  ";

        /// <summary>
        /// Omit the XML declaration
        /// </summary>
        [Parameter]
        public SwitchParameter OmitXmlDeclaration { get; set; }

        /// <summary>
        /// Return the created file info
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", $"Exporting Unattend XML configuration to {Path}");

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

                // Get the encoding
                var encoding = GetTextEncoding();
                LoggingService.WriteVerbose(this, "General", $"Using encoding: {encoding.EncodingName}");

                // Create directory if it doesn't exist
                var directory = System.IO.Path.GetDirectoryName(resolvedPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    LoggingService.WriteVerbose(this, "General", $"Created directory: {directory}");
                }

                // Save the configuration using the built-in method
                Configuration.SaveToFile(resolvedPath, encoding);

                LoggingService.WriteVerbose(this, "General", $"Successfully exported Unattend XML configuration to {resolvedPath}");

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

        private Encoding GetTextEncoding()
        {
            return Encoding.ToUpperInvariant() switch
            {
                "UTF8" => System.Text.Encoding.UTF8,
                "UTF16" => System.Text.Encoding.Unicode,
                "UTF32" => System.Text.Encoding.UTF32,
                "ASCII" => System.Text.Encoding.ASCII,
                "UNICODE" => System.Text.Encoding.Unicode,
                "BIGENDIANUNICODE" => System.Text.Encoding.BigEndianUnicode,
                _ => System.Text.Encoding.UTF8
            };
        }
    }
}
