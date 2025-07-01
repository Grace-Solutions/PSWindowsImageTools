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
        /// Filter by operating system (Windows 10, Windows 11, Windows Server 2019, etc.)
        /// </summary>
        [Parameter(
            HelpMessage = "Filter by operating system (Windows 10, Windows 11, Windows Server 2019, etc.)")]
        public string OperatingSystem { get; set; } = string.Empty;

        /// <summary>
        /// Filter by release ID (21H2, 22H2, 23H2, 24H2, etc.)
        /// </summary>
        [Parameter(
            HelpMessage = "Filter by release ID (21H2, 22H2, 23H2, 24H2, etc.)")]
        public string ReleaseId { get; set; } = string.Empty;

        /// <summary>
        /// Filter by build number (19041, 19042, 22000, 22621, etc.)
        /// </summary>
        [Parameter(
            HelpMessage = "Filter by build number (19041, 19042, 22000, 22621, etc.)")]
        public int BuildNumber { get; set; }

        /// <summary>
        /// Filter by KB article number (KB5000001, etc.)
        /// </summary>
        [Parameter(
            HelpMessage = "Filter by KB article number (KB5000001, etc.)")]
        public string KBArticle { get; set; } = string.Empty;

        /// <summary>
        /// Filter by version string (10.0.19041.1234, etc.)
        /// </summary>
        [Parameter(
            HelpMessage = "Filter by version string (10.0.19041.1234, etc.)")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Include only LTSC/LTSB releases
        /// </summary>
        [Parameter(
            HelpMessage = "Include only LTSC/LTSB releases")]
        public SwitchParameter LTSCOnly { get; set; }

        /// <summary>
        /// Include only Client operating systems (exclude Server)
        /// </summary>
        [Parameter(
            HelpMessage = "Include only Client operating systems (exclude Server)")]
        public SwitchParameter ClientOnly { get; set; }

        /// <summary>
        /// Include only Server operating systems (exclude Client)
        /// </summary>
        [Parameter(
            HelpMessage = "Include only Server operating systems (exclude Client)")]
        public SwitchParameter ServerOnly { get; set; }

        /// <summary>
        /// Get the latest release for each operating system/release ID combination
        /// </summary>
        [Parameter(
            HelpMessage = "Get the latest release for each operating system/release ID combination")]
        public SwitchParameter Latest { get; set; }

        /// <summary>
        /// Return only releases that have KB articles
        /// </summary>
        [Parameter(
            HelpMessage = "Return only releases that have KB articles")]
        public SwitchParameter WithKBOnly { get; set; }

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

                // Validate conflicting parameters
                if (ClientOnly.IsPresent && ServerOnly.IsPresent)
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new ArgumentException("ClientOnly and ServerOnly parameters cannot be used together"),
                        "ConflictingParameters",
                        ErrorCategory.InvalidArgument,
                        null));
                    return;
                }

                LoggingService.WriteVerbose(this, "Fetching Windows release information from Microsoft sources...");

                // Get release information using the Windows Release History service
                var releaseInfoService = new WindowsReleaseHistoryService(HttpClient, this, ContinueOnError.IsPresent);
                var allReleases = Task.Run(async () => await releaseInfoService.GetWindowsReleaseHistoryAsync()).Result;

                LoggingService.WriteVerbose(this, $"Retrieved {allReleases.Count} release records");

                // Apply filters
                var filteredReleases = ApplyFilters(allReleases);

                LoggingService.WriteVerbose(this, $"Filtered to {filteredReleases.Count} matching releases");

                // Apply Latest filter if requested
                if (Latest.IsPresent)
                {
                    filteredReleases = GetLatestReleases(filteredReleases);
                    LoggingService.WriteVerbose(this, $"Latest filter applied, returning {filteredReleases.Count} releases");
                }

                // Output results
                foreach (var release in filteredReleases.OrderBy(r => r.OperatingSystem).ThenBy(r => r.ReleaseId))
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
        /// Applies filters to the release information
        /// </summary>
        private List<WindowsReleaseInfo> ApplyFilters(List<WindowsReleaseInfo> releases)
        {
            var filtered = releases.AsEnumerable();

            // Operating System filter
            if (!string.IsNullOrEmpty(OperatingSystem))
            {
                var normalizedFilter = FormatUtilityService.NormalizeOperatingSystemName(OperatingSystem);
                filtered = filtered.Where(r => FormatUtilityService.ContainsIgnoreCase(
                    FormatUtilityService.NormalizeOperatingSystemName(r.OperatingSystem), normalizedFilter));
            }

            // Release ID filter
            if (!string.IsNullOrEmpty(ReleaseId))
            {
                var normalizedFilter = FormatUtilityService.NormalizeReleaseId(ReleaseId);
                filtered = filtered.Where(r => FormatUtilityService.NormalizeReleaseId(r.ReleaseId)
                    .Equals(normalizedFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Build Number filter
            if (BuildNumber > 0)
            {
                filtered = filtered.Where(r => r.InitialReleaseVersion.Build == BuildNumber ||
                                              r.Releases.Any(rel => rel.Version.Build == BuildNumber));
            }

            // KB Article filter
            if (!string.IsNullOrEmpty(KBArticle))
            {
                var normalizedKB = FormatUtilityService.NormalizeKBArticle(KBArticle);
                filtered = filtered.Where(r => r.Releases.Any(rel =>
                    FormatUtilityService.NormalizeKBArticle(rel.KBArticle).Equals(normalizedKB, StringComparison.OrdinalIgnoreCase)));
            }

            // Version filter
            if (!string.IsNullOrEmpty(Version))
            {
                filtered = filtered.Where(r => FormatUtilityService.ContainsIgnoreCase(r.InitialReleaseVersion.ToString(), Version) ||
                                              r.Releases.Any(rel => FormatUtilityService.ContainsIgnoreCase(rel.Version.ToString(), Version)));
            }

            // Date filters
            if (After != default)
            {
                filtered = filtered.Where(r => r.Releases.Any(rel => rel.AvailabilityDate >= After.Date));
            }

            if (Before != default)
            {
                filtered = filtered.Where(r => r.Releases.Any(rel => rel.AvailabilityDate <= Before.Date));
            }

            // KB Only filter
            if (WithKBOnly.IsPresent)
            {
                filtered = filtered.Where(r => r.Releases.Any(rel => !string.IsNullOrEmpty(rel.KBArticle)));
            }

            // LTSC Only filter
            if (LTSCOnly.IsPresent)
            {
                filtered = filtered.Where(r => r.HasLongTermServicingBuild);
            }

            // Client/Server filters
            if (ClientOnly.IsPresent)
            {
                filtered = filtered.Where(r => r.Type.Equals("Client", StringComparison.OrdinalIgnoreCase));
            }
            else if (ServerOnly.IsPresent)
            {
                filtered = filtered.Where(r => r.Type.Equals("Server", StringComparison.OrdinalIgnoreCase));
            }

            return filtered.ToList();
        }

        /// <summary>
        /// Gets the latest release for each operating system/release ID combination
        /// </summary>
        private List<WindowsReleaseInfo> GetLatestReleases(List<WindowsReleaseInfo> releases)
        {
            return releases
                .GroupBy(r => new { r.OperatingSystem, r.ReleaseId })
                .Select(g => g.OrderByDescending(r => r.Releases.Max(rel => rel.AvailabilityDate)).First())
                .ToList();
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
