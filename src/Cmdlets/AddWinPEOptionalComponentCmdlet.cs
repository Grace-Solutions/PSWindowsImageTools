using System;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Installs WinPE Optional Components into mounted boot images
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "WinPEOptionalComponent")]
    [OutputType(typeof(OptionalComponentInstallationResult[]))]
    public class AddWinPEOptionalComponentCmdlet : PSCmdlet
    {
        private const string ComponentName = "Add-WinPEOptionalComponent";

        /// <summary>
        /// Mounted boot images to install components into (from Mount-WindowsImageList)
        /// </summary>
        [Parameter(
            Mandatory = true,
            Position = 0,
            ValueFromPipeline = true,
            ParameterSetName = "ByMountedImage",
            HelpMessage = "Mounted boot images to install components into (from Mount-WindowsImageList)")]
        [ValidateNotNull]
        public MountedWindowsImage[] MountedImages { get; set; } = null!;

        /// <summary>
        /// Optional components to install (from Get-WinPEOptionalComponent)
        /// </summary>
        [Parameter(
            Mandatory = true,
            Position = 1,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Optional components to install (from Get-WinPEOptionalComponent)")]
        [ValidateNotNull]
        public WinPEOptionalComponent[] Components { get; set; } = null!;

        /// <summary>
        /// Continue processing other components if one fails
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Continue processing other components if one fails")]
        public SwitchParameter ContinueOnError { get; set; }



        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName, "Install WinPE Optional Components");

                // Validate inputs
                ValidateInputs();

                LoggingService.WriteVerbose(this, ComponentName, 
                    $"Installing {Components.Length} components into {MountedImages.Length} mounted images");

                // Log component details
                LogComponentDetails();

                // Install components using the service
                var componentService = new OptionalComponentService();
                var results = componentService.InstallOptionalComponents(
                    MountedImages,
                    Components,
                    ContinueOnError.IsPresent,
                    true, // Always validate installation
                    this);

                // Output results
                foreach (var result in results)
                {
                    WriteObject(result);
                }

                // Generate summary
                GenerateInstallationSummary(results);

                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Component installation", operationStartTime, "Component installation completed");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, "Failed to install WinPE Optional Components", ex);
                WriteError(new ErrorRecord(ex, "ComponentInstallationError", ErrorCategory.NotSpecified, null));
            }
        }

        /// <summary>
        /// Validates input parameters
        /// </summary>
        private void ValidateInputs()
        {
            // Validate mounted images
            foreach (var mountedImage in MountedImages)
            {
                if (mountedImage.MountPath?.Exists != true)
                {
                    throw new ArgumentException($"Mount path does not exist: {mountedImage.MountPath?.FullName ?? "Unknown"}");
                }

                if (mountedImage.IsReadOnly)
                {
                    throw new ArgumentException($"Mounted image is read-only: {mountedImage.ImageName}. Use -ReadWrite when mounting.");
                }

                // Validate this is a boot image (typically index 1 or 2)
                if (mountedImage.ImageIndex > 2)
                {
                    WriteWarning($"Image index {mountedImage.ImageIndex} may not be a boot image. WinPE Optional Components are typically installed into boot images (index 1 or 2).");
                }
            }

            // Validate components
            foreach (var component in Components)
            {
                if (!component.ComponentFile.Exists)
                {
                    throw new ArgumentException($"Component file does not exist: {component.ComponentFile.FullName}");
                }

                if (component.SizeInBytes == 0)
                {
                    WriteWarning($"Component {component.Name} has unknown size, installation may fail.");
                }
            }

            LoggingService.WriteVerbose(this, ComponentName, "Input validation completed successfully");
        }

        /// <summary>
        /// Logs details about components to be installed
        /// </summary>
        private void LogComponentDetails()
        {
            LoggingService.WriteVerbose(this, ComponentName, "=== Component Installation Details ===");
            
            // Group components by category
            var componentsByCategory = Components.GroupBy(c => c.Category).OrderBy(g => g.Key);
            
            foreach (var categoryGroup in componentsByCategory)
            {
                LoggingService.WriteVerbose(this, ComponentName, $"Category: {categoryGroup.Key}");
                
                foreach (var component in categoryGroup.OrderBy(c => c.Name))
                {
                    LoggingService.WriteVerbose(this, ComponentName, 
                        $"  - {component.Name} ({component.Architecture}) - {component.SizeFormatted}");
                }
            }

            // Calculate total size
            var totalSize = Components.Sum(c => c.SizeInBytes);
            var totalSizeFormatted = FormatSize(totalSize);
            
            LoggingService.WriteVerbose(this, ComponentName, $"Total size: {totalSizeFormatted}");

            // Log target images
            LoggingService.WriteVerbose(this, ComponentName, "=== Target Images ===");
            foreach (var image in MountedImages)
            {
                LoggingService.WriteVerbose(this, ComponentName, 
                    $"  - {image.ImageName} (Index {image.ImageIndex}) at {image.MountPath?.FullName ?? "Unknown"}");
            }
        }

        /// <summary>
        /// Generates and displays installation summary
        /// </summary>
        private void GenerateInstallationSummary(System.Collections.Generic.List<OptionalComponentInstallationResult> results)
        {
            var totalImages = results.Count;
            var totalComponents = Components.Length;
            var totalOperations = totalImages * totalComponents;
            
            var totalSuccessful = results.Sum(r => r.SuccessfulComponents.Count);
            var totalFailed = results.Sum(r => r.FailedComponents.Count);
            var totalSkipped = results.Sum(r => r.SkippedComponents.Count);
            
            var overallSuccessRate = totalOperations > 0 ? (double)totalSuccessful / totalOperations * 100 : 0;
            var averageDuration = results.Count > 0 ? results.Average(r => r.Duration.TotalSeconds) : 0;

            LoggingService.WriteVerbose(this, ComponentName, "=== Installation Summary ===");
            LoggingService.WriteVerbose(this, ComponentName, $"Images processed: {totalImages}");
            LoggingService.WriteVerbose(this, ComponentName, $"Components per image: {totalComponents}");
            LoggingService.WriteVerbose(this, ComponentName, $"Total operations: {totalOperations}");
            LoggingService.WriteVerbose(this, ComponentName, $"Successful: {totalSuccessful} ({totalSuccessful}/{totalOperations} = {overallSuccessRate:F1}%)");
            LoggingService.WriteVerbose(this, ComponentName, $"Failed: {totalFailed}");
            LoggingService.WriteVerbose(this, ComponentName, $"Skipped: {totalSkipped}");
            LoggingService.WriteVerbose(this, ComponentName, $"Average duration per image: {averageDuration:F1} seconds");

            // Per-image breakdown
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                LoggingService.WriteVerbose(this, ComponentName, 
                    $"Image {i + 1} ({result.MountedImage.ImageName}): " +
                    $"{result.SuccessfulComponents.Count} successful, " +
                    $"{result.FailedComponents.Count} failed, " +
                    $"{result.SkippedComponents.Count} skipped " +
                    $"({result.SuccessRate:F1}% success rate)");
            }

            // Error summary
            var allErrors = results.SelectMany(r => r.Errors).ToList();
            if (allErrors.Count > 0)
            {
                LoggingService.WriteVerbose(this, ComponentName, "=== Errors Encountered ===");
                foreach (var error in allErrors.Take(10)) // Limit to first 10 errors
                {
                    LoggingService.WriteVerbose(this, ComponentName, $"  - {error}");
                }
                
                if (allErrors.Count > 10)
                {
                    LoggingService.WriteVerbose(this, ComponentName, $"  ... and {allErrors.Count - 10} more errors");
                }
            }

            // Write summary to output
            if (totalFailed == 0)
            {
                WriteInformation(new InformationRecord(
                    $"✓ Successfully installed {totalSuccessful} components across {totalImages} images",
                    "InstallationSuccess"));
            }
            else if (totalSuccessful > 0)
            {
                WriteWarning($"⚠ Partial success: {totalSuccessful} successful, {totalFailed} failed across {totalImages} images");
            }
            else
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException($"✗ All component installations failed ({totalFailed} failures)"),
                    "AllInstallationsFailed",
                    ErrorCategory.OperationStopped,
                    null));
            }
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
