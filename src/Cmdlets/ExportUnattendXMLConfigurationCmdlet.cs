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
        /// Output file for the XML file
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNull]
        public FileInfo OutputFile { get; set; } = null!;

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
                LoggingService.WriteVerbose(this, "General", $"Exporting Unattend XML configuration to {OutputFile.FullName}");

                // Check if file exists and Force is not specified
                if (OutputFile.Exists && !Force.IsPresent)
                {
                    WriteError(new ErrorRecord(
                        new InvalidOperationException($"File already exists: {OutputFile.FullName}. Use -Force to overwrite."),
                        "FileExists",
                        ErrorCategory.ResourceExists,
                        OutputFile));
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
                if (OutputFile.Directory != null && !OutputFile.Directory.Exists)
                {
                    OutputFile.Directory.Create();
                    LoggingService.WriteVerbose(this, "General", $"Created directory: {OutputFile.Directory.FullName}");
                }

                // Save the configuration using the built-in method
                Configuration.SaveToFile(OutputFile.FullName, encoding);

                LoggingService.WriteVerbose(this, "General", $"Successfully exported Unattend XML configuration to {OutputFile.FullName}");

                if (PassThru.IsPresent)
                {
                    OutputFile.Refresh(); // Refresh to get updated file info
                    WriteObject(OutputFile);
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
