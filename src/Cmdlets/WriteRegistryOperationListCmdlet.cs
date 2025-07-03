using System;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Cmdlet to write registry operations to mounted Windows images
    /// </summary>
    [Cmdlet(VerbsCommunications.Write, "RegistryOperationList")]
    [OutputType(typeof(RegistryOperationResult))]
    public class WriteRegistryOperationListCmdlet : PSCmdlet
    {
        /// <summary>
        /// Mounted Windows images to apply operations to
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public MountedWindowsImage[] MountedImages { get; set; } = Array.Empty<MountedWindowsImage>();

        /// <summary>
        /// Registry operations to apply
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public RegistryOperation[] Operations { get; set; } = Array.Empty<RegistryOperation>();

        /// <summary>
        /// Continue processing even if some operations fail
        /// </summary>
        [Parameter]
        public SwitchParameter ContinueOnError { get; set; }

        /// <summary>
        /// Test mode - validate operations without applying them
        /// </summary>
        [Parameter]
        public SwitchParameter WhatIf { get; set; }

        private RegistryApplicationService _registryService = new RegistryApplicationService();

        protected override void BeginProcessing()
        {
            LoggingService.WriteVerbose(this, 
                $"Starting registry operation application - {Operations.Length} operations for {MountedImages.Length} images");

            if (WhatIf.IsPresent)
            {
                WriteWarning("WhatIf mode: Operations will be validated but not applied");
            }

            // Validate mounted images
            foreach (var image in MountedImages)
            {
                if (image.Status != Models.MountStatus.Mounted)
                {
                    throw new InvalidOperationException($"Image {image.ImageName} is not mounted (Status: {image.Status})");
                }
            }

            // Validate operations
            ValidateOperations();
        }

        protected override void ProcessRecord()
        {
            try
            {
                if (WhatIf.IsPresent)
                {
                    // In WhatIf mode, just show what would be done
                    ShowWhatIfOperations();
                    return;
                }

                var results = _registryService.ApplyOperations(MountedImages, Operations, this);

                foreach (var result in results)
                {
                    WriteObject(result);

                    // Write summary information
                    if (result.SuccessCount > 0)
                    {
                        LoggingService.WriteVerbose(this,
                            $"Successfully applied {result.SuccessCount} operations to {result.MountedImage.ImageName}");
                    }

                    if (result.FailureCount > 0)
                    {
                        var message = $"Failed to apply {result.FailureCount} operations to {result.MountedImage.ImageName}";
                        
                        if (ContinueOnError.IsPresent)
                        {
                            WriteWarning(message);
                        }
                        else
                        {
                            throw new InvalidOperationException(message);
                        }
                    }
                }

                LoggingService.WriteVerbose(this, "Registry operation application completed");
            }
            catch (Exception ex)
            {
                var errorRecord = new ErrorRecord(
                    ex,
                    "RegistryOperationApplicationFailed",
                    ErrorCategory.InvalidOperation,
                    null);

                if (ContinueOnError.IsPresent)
                {
                    WriteError(errorRecord);
                }
                else
                {
                    ThrowTerminatingError(errorRecord);
                }
            }
        }

        /// <summary>
        /// Validates the registry operations
        /// </summary>
        private void ValidateOperations()
        {
            var invalidOperations = Operations.Where(op => 
                string.IsNullOrEmpty(op.Hive) || 
                string.IsNullOrEmpty(op.Key)).ToList();

            if (invalidOperations.Any())
            {
                var message = $"Found {invalidOperations.Count} invalid operations with missing hive or key information";
                throw new ArgumentException(message);
            }

            // Check for potentially dangerous operations
            var dangerousOperations = Operations.Where(op =>
                op.Operation == RegistryOperationType.RemoveKey &&
                (op.Key.Equals("SOFTWARE", StringComparison.OrdinalIgnoreCase) ||
                 op.Key.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                 op.Key.Equals("", StringComparison.OrdinalIgnoreCase))).ToList();

            if (dangerousOperations.Any())
            {
                WriteWarning($"Found {dangerousOperations.Count} potentially dangerous key deletion operations");
                foreach (var op in dangerousOperations)
                {
                    WriteWarning($"  - Remove key: {op.GetFullPath()}");
                }
            }
        }

        /// <summary>
        /// Shows what operations would be performed in WhatIf mode
        /// </summary>
        private void ShowWhatIfOperations()
        {
            WriteInformation(new InformationRecord("Registry Operations to be Applied:", "WhatIf"));
            
            var operationsByImage = MountedImages.Select(image => new
            {
                Image = image,
                Operations = Operations.GroupBy(op => op.GetMappedHive()).ToList()
            });

            foreach (var imageGroup in operationsByImage)
            {
                WriteInformation(new InformationRecord($"\nImage: {imageGroup.Image.ImageName}", "WhatIf"));
                WriteInformation(new InformationRecord($"Mount Path: {imageGroup.Image.MountPath}", "WhatIf"));

                foreach (var hiveGroup in imageGroup.Operations)
                {
                    WriteInformation(new InformationRecord($"\n  Hive: {hiveGroup.Key}", "WhatIf"));
                    
                    foreach (var operation in hiveGroup)
                    {
                        var operationDesc = operation.Operation switch
                        {
                            RegistryOperationType.Create => $"CREATE: {operation.GetFullPath()}\\{operation.ValueName} = {operation.Value}",
                            RegistryOperationType.Modify => $"MODIFY: {operation.GetFullPath()}\\{operation.ValueName} = {operation.Value}",
                            RegistryOperationType.Remove => $"REMOVE: {operation.GetFullPath()}\\{operation.ValueName}",
                            RegistryOperationType.RemoveKey => $"REMOVE KEY: {operation.GetFullPath()}",
                            _ => operation.ToString()
                        };

                        WriteInformation(new InformationRecord($"    {operationDesc}", "WhatIf"));
                    }
                }
            }

            WriteInformation(new InformationRecord($"\nTotal: {Operations.Length} operations across {MountedImages.Length} images", "WhatIf"));
        }
    }
}
