using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Loads and parses Windows Unattend XML configuration files with enhanced navigation
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "UnattendXMLConfiguration")]
    [OutputType(typeof(UnattendXMLConfiguration))]
    public class GetUnattendXMLConfigurationCmdlet : PSCmdlet
    {
        /// <summary>
        /// Path to Unattend XML file or directory containing XML files
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Include subdirectories when scanning for XML files
        /// </summary>
        [Parameter]
        public SwitchParameter Recurse { get; set; }

        /// <summary>
        /// Validate the configuration after loading
        /// </summary>
        [Parameter]
        public SwitchParameter Validate { get; set; }

        /// <summary>
        /// Show detailed component information with XPath
        /// </summary>
        [Parameter]
        public SwitchParameter ShowComponents { get; set; }

        /// <summary>
        /// Show all XML elements with their XPath for navigation
        /// </summary>
        [Parameter]
        public SwitchParameter ShowElements { get; set; }

        /// <summary>
        /// Filter elements by name (supports wildcards)
        /// </summary>
        [Parameter]
        public string ElementFilter { get; set; } = string.Empty;

        private UnattendXMLService _unattendService = new UnattendXMLService();

        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", "Starting Unattend XML configuration loading");

                // Resolve the path
                var resolvedPaths = GetResolvedProviderPathFromPSPath(Path, out var provider);
                if (provider.Name != "FileSystem")
                {
                    WriteError(new ErrorRecord(
                        new ArgumentException("Path must be a file system path"),
                        "InvalidPath",
                        ErrorCategory.InvalidArgument,
                        Path));
                    return;
                }

                foreach (var resolvedPath in resolvedPaths)
                {
                    ProcessPath(resolvedPath);
                }

                LoggingService.WriteVerbose(this, "General", "Unattend XML configuration loading completed");
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "UnattendXMLConfigurationError", ErrorCategory.NotSpecified, Path));
            }
        }

        private void ProcessPath(string path)
        {
            if (File.Exists(path))
            {
                // Single file
                ProcessFile(path);
            }
            else if (Directory.Exists(path))
            {
                // Directory
                ProcessDirectory(path);
            }
            else
            {
                WriteError(new ErrorRecord(
                    new FileNotFoundException($"Path not found: {path}"),
                    "PathNotFound",
                    ErrorCategory.ObjectNotFound,
                    path));
            }
        }

        private void ProcessFile(string filePath)
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", $"Loading Unattend XML configuration from file: {filePath}");

                var configuration = _unattendService.LoadConfiguration(filePath, this);

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

                if (ShowComponents.IsPresent)
                {
                    WriteVerbose($"Configuration passes: {string.Join(", ", configuration.ConfigurationPasses)}");
                    WriteVerbose($"Components found: {configuration.Components.Count}");
                    foreach (var component in configuration.Components)
                    {
                        WriteVerbose($"  - {component.Name} ({component.Pass}) - XPath: {component.XPath}");
                    }
                }

                if (ShowElements.IsPresent)
                {
                    var elements = configuration.Elements;
                    if (!string.IsNullOrEmpty(ElementFilter))
                    {
                        var wildcard = new WildcardPattern(ElementFilter, WildcardOptions.IgnoreCase);
                        elements = elements.Where(e => wildcard.IsMatch(e.Name) || wildcard.IsMatch(e.FullName)).ToList();
                    }

                    WriteVerbose($"XML Elements found: {elements.Count}");
                    foreach (var element in elements.Take(20)) // Limit to first 20 for readability
                    {
                        var valuePreview = element.Value.Length > 50 ? element.Value.Substring(0, 50) + "..." : element.Value;
                        WriteVerbose($"  - {element.Name}: '{valuePreview}' - XPath: {element.XPath}");
                    }
                    if (elements.Count > 20)
                    {
                        WriteVerbose($"  ... and {elements.Count - 20} more elements (use -ElementFilter to narrow down)");
                    }
                }

                WriteObject(configuration);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "FileProcessingError", ErrorCategory.NotSpecified, filePath));
            }
        }

        private void ProcessDirectory(string directoryPath)
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", $"Scanning directory for Unattend XML configurations: {directoryPath}");

                var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var xmlFiles = Directory.GetFiles(directoryPath, "*.xml", searchOption);

                LoggingService.WriteVerbose(this, "General", $"Found {xmlFiles.Length} XML files to process");

                foreach (var file in xmlFiles)
                {
                    ProcessFile(file);
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "DirectoryProcessingError", ErrorCategory.NotSpecified, directoryPath));
            }
        }

        protected override void EndProcessing()
        {
            _unattendService = null!;
        }
    }
}
