using System;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Detects installed Windows Assessment and Deployment Kit (ADK) installations
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "ADKInstallation")]
    [OutputType(typeof(ADKInfo[]))]
    public class GetADKInstallationCmdlet : PSCmdlet
    {
        private const string ComponentName = "Get-ADKInstallation";

        /// <summary>
        /// Return only the latest version if multiple ADK installations are found
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Return only the latest version if multiple ADK installations are found")]
        public SwitchParameter Latest { get; set; }

        /// <summary>
        /// Minimum required version of ADK
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Minimum required version of ADK (e.g., '10.0.22000.1')")]
        [ValidateNotNull]
        public Version? MinimumVersion { get; set; }

        /// <summary>
        /// Require WinPE add-on to be installed
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Require WinPE add-on to be installed")]
        public SwitchParameter RequireWinPE { get; set; }

        /// <summary>
        /// Require Deployment Tools to be installed
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Require Deployment Tools to be installed")]
        public SwitchParameter RequireDeploymentTools { get; set; }

        /// <summary>
        /// Specific architecture support required (x86, amd64, arm64)
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Specific architecture support required (x86, amd64, arm64)")]
        [ValidateSet("x86", "amd64", "arm64")]
        public string? RequiredArchitecture { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName, "Detect ADK Installations");

                LoggingService.WriteVerbose(this, ComponentName, "Starting ADK installation detection");

                // Create ADK service and detect installations
                var adkService = new ADKService();
                var adkInstallations = adkService.DetectADKInstallations(this);

                if (adkInstallations.Count == 0)
                {
                    LoggingService.WriteWarning(this, ComponentName, "No ADK installations found on this system");
                    WriteWarning("No Windows ADK installations were detected. Please ensure ADK is installed.");
                    return;
                }

                LoggingService.WriteVerbose(this, ComponentName, $"Found {adkInstallations.Count} ADK installations");

                // Apply filters
                var filteredInstallations = ApplyFilters(adkInstallations);

                if (filteredInstallations.Count == 0)
                {
                    LoggingService.WriteWarning(this, ComponentName, "No ADK installations match the specified criteria");
                    WriteWarning("No ADK installations match the specified filtering criteria.");
                    return;
                }

                // Apply Latest filter if requested
                if (Latest.IsPresent)
                {
                    var latestInstallation = filteredInstallations.OrderByDescending(adk => adk.Version).First();
                    filteredInstallations = new[] { latestInstallation }.ToList();
                    
                    LoggingService.WriteVerbose(this, ComponentName, 
                        $"Latest ADK version selected: {latestInstallation.Version}");
                }

                // Output results
                foreach (var installation in filteredInstallations.OrderByDescending(adk => adk.Version))
                {
                    WriteObject(installation);
                }

                // Summary logging
                var summaryMessage = $"Returned {filteredInstallations.Count} ADK installation(s)";
                if (Latest.IsPresent && filteredInstallations.Count == 1)
                {
                    summaryMessage += $" (latest version: {filteredInstallations[0].Version})";
                }

                LoggingService.WriteVerbose(this, ComponentName, summaryMessage);
                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "ADK detection", operationStartTime, "ADK detection completed");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, "Failed to detect ADK installations", ex);
                WriteError(new ErrorRecord(ex, "ADKDetectionError", ErrorCategory.NotSpecified, null));
            }
        }

        /// <summary>
        /// Applies filtering criteria to ADK installations
        /// </summary>
        private System.Collections.Generic.List<ADKInfo> ApplyFilters(System.Collections.Generic.List<ADKInfo> installations)
        {
            var filtered = installations.AsEnumerable();

            // Apply minimum version filter
            if (MinimumVersion != null)
            {
                filtered = filtered.Where(adk => adk.Version >= MinimumVersion);
                LoggingService.WriteVerbose(this, ComponentName, 
                    $"Applied minimum version filter: {MinimumVersion}");
            }

            // Apply WinPE requirement filter
            if (RequireWinPE.IsPresent)
            {
                filtered = filtered.Where(adk => adk.HasWinPEAddon);
                LoggingService.WriteVerbose(this, ComponentName, "Applied WinPE requirement filter");
            }

            // Apply Deployment Tools requirement filter
            if (RequireDeploymentTools.IsPresent)
            {
                filtered = filtered.Where(adk => adk.HasDeploymentTools);
                LoggingService.WriteVerbose(this, ComponentName, "Applied Deployment Tools requirement filter");
            }

            // Apply architecture requirement filter
            if (!string.IsNullOrEmpty(RequiredArchitecture))
            {
                filtered = filtered.Where(adk => adk.SupportedArchitectures.Contains(RequiredArchitecture, StringComparer.OrdinalIgnoreCase));
                LoggingService.WriteVerbose(this, ComponentName, 
                    $"Applied architecture requirement filter: {RequiredArchitecture}");
            }

            return filtered.ToList();
        }


    }
}
