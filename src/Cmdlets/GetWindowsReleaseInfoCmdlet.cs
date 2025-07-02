using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Cmdlet for fetching Windows release information from Microsoft's release history
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "WindowsReleaseInfo")]
    [OutputType(typeof(WindowsReleaseInfo[]))]
    public class GetWindowsReleaseInfoCmdlet : PSCmdlet
    {


        /// <summary>
        /// Filter releases after this date
        /// </summary>
        [Parameter(
            HelpMessage = "Filter releases after this date")]
        public DateTime After { get; set; }

        /// <summary>
        /// Filter releases before this date
        /// </summary>
        [Parameter(
            HelpMessage = "Filter releases before this date")]
        public DateTime Before { get; set; }

        /// <summary>
        /// Include detailed release information for each update
        /// </summary>
        [Parameter(
            HelpMessage = "Include detailed release information for each update")]
        public SwitchParameter Detailed { get; set; }

        /// <summary>
        /// Continue processing on errors instead of stopping
        /// </summary>
        [Parameter(
            HelpMessage = "Continue processing on errors instead of stopping")]
        public SwitchParameter ContinueOnError { get; set; }

        private const string ComponentName = "WindowsReleaseInfo";
        private static readonly HttpClient HttpClient = new HttpClient();

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName, "Get Windows Release Info");



                LoggingService.WriteVerbose(this, "Fetching Windows release information from Microsoft sources...");

                // Get release information using the Windows Release History service
                var releaseInfoService = new WindowsReleaseHistoryService(HttpClient, this, ContinueOnError.IsPresent);
                var allReleases = releaseInfoService.GetWindowsReleaseHistory();

                // Write any collected warnings/errors from the service
                releaseInfoService.WriteCollectedMessages();

                LoggingService.WriteVerbose(this, $"Retrieved {allReleases.Count} release records");

                // Apply date filters if specified
                var filteredReleases = ApplyDateFilters(allReleases);

                LoggingService.WriteVerbose(this, $"Filtered to {filteredReleases.Count} matching releases");

                // Output results
                foreach (var release in filteredReleases.OrderBy(r => r.OperatingSystem).ThenBy(r => r.ReleaseID))
                {
                    WriteObject(release);
                }

                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Get Windows Release Info", operationStartTime,
                    $"Retrieved {filteredReleases.Count} Windows release records");
            }
            catch (Exception ex)
            {
                LoggingService.LogOperationFailure(this, ComponentName, ex);
                
                if (ContinueOnError.IsPresent)
                {
                    WriteError(new ErrorRecord(ex, "GetWindowsReleaseInfoFailed", ErrorCategory.NotSpecified, null));
                }
                else
                {
                    ThrowTerminatingError(new ErrorRecord(ex, "GetWindowsReleaseInfoFailed", ErrorCategory.NotSpecified, null));
                }
            }
        }

        /// <summary>
        /// Applies date filters to the release information
        /// </summary>
        private List<WindowsReleaseInfo> ApplyDateFilters(List<WindowsReleaseInfo> releases)
        {
            var filtered = releases.AsEnumerable();

            // Date filters
            if (After != default)
            {
                filtered = filtered.Where(r => r.Releases.Any(rel => rel.AvailabilityDate >= After.Date));
            }

            if (Before != default)
            {
                filtered = filtered.Where(r => r.Releases.Any(rel => rel.AvailabilityDate <= Before.Date));
            }

            return filtered.ToList();
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        protected override void EndProcessing()
        {
            base.EndProcessing();
        }
    }
}
