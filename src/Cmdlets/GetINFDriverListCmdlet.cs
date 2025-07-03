using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Scans directories for INF driver files with optional parsing and recursion
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "INFDriverList")]
    [OutputType(typeof(INFDriverInfo[]))]
    public class GetINFDriverListCmdlet : PSCmdlet
    {
        private const string ComponentName = "Get-INFDriverList";
        private readonly List<DirectoryInfo> _allDirectories = new List<DirectoryInfo>();

        /// <summary>
        /// One or more directories to scan for INF files
        /// </summary>
        [Parameter(
            Mandatory = true,
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "One or more directories to scan for INF driver files")]
        [ValidateNotNull]
        public DirectoryInfo[] Path { get; set; } = Array.Empty<DirectoryInfo>();

        /// <summary>
        /// Scan directories recursively
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Scan directories recursively for INF files")]
        public SwitchParameter Recurse { get; set; }

        /// <summary>
        /// Parse INF files for detailed metadata
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Parse INF files to extract driver metadata")]
        public SwitchParameter ParseINF { get; set; }

        /// <summary>
        /// Processes pipeline input
        /// </summary>
        protected override void ProcessRecord()
        {
            // Validate that all directories exist
            foreach (var directory in Path)
            {
                if (!directory.Exists)
                {
                    var errorMessage = $"Directory does not exist: {directory.FullName}";
                    var errorRecord = new ErrorRecord(
                        new DirectoryNotFoundException(errorMessage),
                        "DirectoryNotFound",
                        ErrorCategory.ObjectNotFound,
                        directory);
                    WriteError(errorRecord);
                    continue;
                }

                _allDirectories.Add(directory);
            }
        }

        /// <summary>
        /// Performs the INF driver scanning operation
        /// </summary>
        protected override void EndProcessing()
        {
            if (_allDirectories.Count == 0)
            {
                LoggingService.WriteWarning(this, "No valid directories provided for INF driver scanning");
                return;
            }

                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName, "Scan INF Drivers",
                $"Scanning {_allDirectories.Count} directories (Recurse: {Recurse.IsPresent}, Parse: {ParseINF.IsPresent})");

            try
            {
                // Scan for INF drivers
                var infDriverService = new INFDriverService();
                var drivers = infDriverService.ScanForINFDrivers(
                    _allDirectories.ToArray(),
                    Recurse.IsPresent,
                    ParseINF.IsPresent,
                    this);

                // Output results
                foreach (var driver in drivers)
                {
                    WriteObject(driver);
                }

                // Summary
                var totalDrivers = drivers.Count;
                var parsedDrivers = drivers.Count(d => d.ParsedInfo != null);
                var parseErrors = drivers.Where(d => d.ParsedInfo?.ParseErrors.Count > 0).Count();

                var summaryMessage = $"Found {totalDrivers} INF files";
                if (ParseINF.IsPresent)
                {
                    summaryMessage += $", {parsedDrivers} parsed successfully";
                    if (parseErrors > 0)
                    {
                        summaryMessage += $", {parseErrors} with parse errors";
                    }
                }

                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Scan INF Drivers", operationStartTime, summaryMessage);
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, $"Failed to scan INF drivers: {ex.Message}", ex);
                throw;
            }
        }
    }
}
