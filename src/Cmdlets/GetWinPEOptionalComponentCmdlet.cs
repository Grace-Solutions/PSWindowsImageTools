using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Gets available WinPE Optional Components from ADK installation
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "WinPEOptionalComponent")]
    [OutputType(typeof(WinPEOptionalComponent[]))]
    public class GetWinPEOptionalComponentCmdlet : PSCmdlet
    {
        private const string ComponentName = "Get-WinPEOptionalComponent";

        /// <summary>
        /// ADK installation to scan for components (from Get-ADKInstallation)
        /// </summary>
        [Parameter(
            Mandatory = false,
            Position = 0,
            ValueFromPipeline = true,
            HelpMessage = "ADK installation to scan for components (from Get-ADKInstallation)")]
        [ValidateNotNull]
        public ADKInfo? ADKInstallation { get; set; }

        /// <summary>
        /// Target architecture for components (x86, amd64, arm64)
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Target architecture for components (x86, amd64, arm64)")]
        [ValidateSet("x86", "amd64", "arm64")]
        public string Architecture { get; set; } = "amd64";

        /// <summary>
        /// Include language pack components
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Include language pack components")]
        public SwitchParameter IncludeLanguagePacks { get; set; }

        /// <summary>
        /// Filter components by category
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Filter components by category")]
        [ValidateSet("Networking", "Storage", "Scripting", "Security", "Hardware", "Development", "Fonts", "Language", "General", "Other")]
        public string[]? Category { get; set; }

        /// <summary>
        /// Filter components by name pattern (supports wildcards)
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Filter components by name pattern (supports wildcards)")]
        public string[]? Name { get; set; }



        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName, "Get WinPE Optional Components");

                // Get ADK installation if not provided
                var adkInstallation = GetADKInstallation();
                if (adkInstallation == null)
                {
                    return;
                }

                LoggingService.WriteVerbose(this, ComponentName, 
                    $"Scanning for {Architecture} components in ADK: {adkInstallation.DisplayName}");

                // Validate architecture support
                if (!adkInstallation.SupportedArchitectures.Contains(Architecture, StringComparer.OrdinalIgnoreCase))
                {
                    var supportedArchs = string.Join(", ", adkInstallation.SupportedArchitectures);
                    WriteError(new ErrorRecord(
                        new ArgumentException($"Architecture '{Architecture}' is not supported by this ADK installation. Supported architectures: {supportedArchs}"),
                        "UnsupportedArchitecture",
                        ErrorCategory.InvalidArgument,
                        Architecture));
                    return;
                }

                // Get components from ADK service
                var adkService = new ADKService();
                var components = adkService.GetAvailableOptionalComponents(
                    adkInstallation, 
                    Architecture, 
                    IncludeLanguagePacks.IsPresent, 
                    this);

                if (components.Count == 0)
                {
                    LoggingService.WriteWarning(this, ComponentName, 
                        $"No optional components found for {Architecture} architecture");
                    WriteWarning($"No WinPE Optional Components found for {Architecture} architecture in the specified ADK installation.");
                    return;
                }

                LoggingService.WriteVerbose(this, ComponentName, 
                    $"Found {components.Count} components before filtering");

                // Apply filters
                var filteredComponents = ApplyFilters(components);

                if (filteredComponents.Count == 0)
                {
                    LoggingService.WriteWarning(this, ComponentName, "No components match the specified criteria");
                    WriteWarning("No components match the specified filtering criteria.");
                    return;
                }

                // Output results
                foreach (var component in filteredComponents.OrderBy(c => c.Category).ThenBy(c => c.Name))
                {
                    WriteObject(component);
                }

                // Summary
                var totalSize = filteredComponents.Sum(c => c.SizeInBytes);
                var totalSizeFormatted = FormatSize(totalSize);
                
                LoggingService.WriteVerbose(this, ComponentName, 
                    $"Returned {filteredComponents.Count} components, total size: {totalSizeFormatted}");

                // Category breakdown
                var categoryBreakdown = filteredComponents
                    .GroupBy(c => c.Category)
                    .Select(g => $"{g.Key}: {g.Count()}")
                    .ToList();

                LoggingService.WriteVerbose(this, ComponentName, 
                    $"Category breakdown: {string.Join(", ", categoryBreakdown)}");

                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Component scan", operationStartTime, "Component scan completed");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, "Failed to get WinPE Optional Components", ex);
                WriteError(new ErrorRecord(ex, "ComponentScanError", ErrorCategory.NotSpecified, null));
            }
        }

        /// <summary>
        /// Gets ADK installation, either from parameter or by auto-detection
        /// </summary>
        private ADKInfo? GetADKInstallation()
        {
            if (ADKInstallation != null)
            {
                return ADKInstallation;
            }

            LoggingService.WriteVerbose(this, ComponentName, "No ADK installation specified, attempting auto-detection");

            var adkService = new ADKService();
            var adkInstallations = adkService.DetectADKInstallations(this);

            if (adkInstallations.Count == 0)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException("No ADK installations found. Please install Windows ADK or specify an ADK installation using the -ADKInstallation parameter."),
                    "NoADKFound",
                    ErrorCategory.ObjectNotFound,
                    null));
                return null;
            }

            // Filter to installations with WinPE support
            var winpeInstallations = adkInstallations.Where(adk => adk.HasWinPEAddon).ToList();
            if (winpeInstallations.Count == 0)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException("No ADK installations with WinPE add-on found. Please install the WinPE add-on for Windows ADK."),
                    "NoWinPEAddon",
                    ErrorCategory.ObjectNotFound,
                    null));
                return null;
            }

            // Use the latest version with WinPE support
            var selectedADK = winpeInstallations.OrderByDescending(adk => adk.Version).First();
            
            LoggingService.WriteVerbose(this, ComponentName, 
                $"Auto-selected ADK installation: {selectedADK.DisplayName} v{selectedADK.Version}");

            return selectedADK;
        }

        /// <summary>
        /// Applies filtering criteria to components
        /// </summary>
        private List<WinPEOptionalComponent> ApplyFilters(List<WinPEOptionalComponent> components)
        {
            var filtered = components.AsEnumerable();

            // Apply category filter
            if (Category != null && Category.Length > 0)
            {
                filtered = filtered.Where(c => Category.Contains(c.Category, StringComparer.OrdinalIgnoreCase));
                LoggingService.WriteVerbose(this, ComponentName, 
                    $"Applied category filter: {string.Join(", ", Category)}");
            }

            // Apply name filter
            if (Name != null && Name.Length > 0)
            {
                filtered = filtered.Where(c => Name.Any(pattern => 
                    new WildcardPattern(pattern, WildcardOptions.IgnoreCase).IsMatch(c.Name) ||
                    new WildcardPattern(pattern, WildcardOptions.IgnoreCase).IsMatch(c.DisplayName)));
                LoggingService.WriteVerbose(this, ComponentName, 
                    $"Applied name filter: {string.Join(", ", Name)}");
            }



            return filtered.ToList();
        }

        /// <summary>
        /// Formats size in bytes to human-readable string
        /// </summary>
        private string FormatSize(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB)
                return $"{bytes / (double)GB:F2} GB";
            if (bytes >= MB)
                return $"{bytes / (double)MB:F2} MB";
            if (bytes >= KB)
                return $"{bytes / (double)KB:F2} KB";
            
            return $"{bytes} bytes";
        }


    }
}
