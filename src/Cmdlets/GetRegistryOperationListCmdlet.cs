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
    /// Cmdlet to parse .reg files and return registry operations
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "RegistryOperationList")]
    [OutputType(typeof(RegistryOperation))]
    public class GetRegistryOperationListCmdlet : PSCmdlet
    {
        /// <summary>
        /// Path to directory containing .reg files or specific .reg files
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string[] Path { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Whether to search recursively for .reg files
        /// </summary>
        [Parameter]
        public SwitchParameter Recurse { get; set; }

        /// <summary>
        /// Filter operations by registry hive
        /// </summary>
        [Parameter]
        [ValidateSet("HKLM", "HKCU", "HKU", "HKCR", "HKEY_LOCAL_MACHINE", "HKEY_CURRENT_USER", "HKEY_USERS", "HKEY_CLASSES_ROOT")]
        public string? FilterHive { get; set; }

        /// <summary>
        /// Filter operations by operation type
        /// </summary>
        [Parameter]
        [ValidateSet("Create", "Modify", "Remove", "RemoveKey")]
        public string? FilterOperation { get; set; }

        private RegistryOperationService _registryService = new RegistryOperationService();

        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.WriteVerbose(this, "Starting registry operation parsing");

                var regFiles = GetRegFiles();
                if (regFiles.Length == 0)
                {
                    WriteWarning("No .reg files found in the specified paths");
                    return;
                }

                LoggingService.WriteVerbose(this, $"Found {regFiles.Length} .reg files to parse");

                var operations = _registryService.ParseRegFiles(regFiles, this);

                // Apply filters if specified
                var filteredOperations = ApplyFilters(operations);

                LoggingService.WriteVerbose(this, 
                    $"Parsed {operations.Count} total operations, {filteredOperations.Count} after filtering");

                foreach (var operation in filteredOperations)
                {
                    WriteObject(operation);
                }

                LoggingService.WriteVerbose(this, "Registry operation parsing completed");
            }
            catch (Exception ex)
            {
                var errorRecord = new ErrorRecord(
                    ex,
                    "RegistryOperationParsingFailed",
                    ErrorCategory.InvalidOperation,
                    null);

                WriteError(errorRecord);
            }
        }

        /// <summary>
        /// Gets all .reg files from the specified paths
        /// </summary>
        private FileInfo[] GetRegFiles()
        {
            var regFiles = new List<FileInfo>();

            foreach (var path in Path)
            {
                try
                {
                    var resolvedPaths = GetResolvedProviderPathFromPSPath(path, out var provider);
                    
                    foreach (var resolvedPath in resolvedPaths)
                    {
                        if (File.Exists(resolvedPath))
                        {
                            var fileInfo = new FileInfo(resolvedPath);
                            if (fileInfo.Extension.Equals(".reg", StringComparison.OrdinalIgnoreCase))
                            {
                                regFiles.Add(fileInfo);
                            }
                        }
                        else if (Directory.Exists(resolvedPath))
                        {
                            var directoryInfo = new DirectoryInfo(resolvedPath);
                            var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                            var foundFiles = directoryInfo.GetFiles("*.reg", searchOption);
                            regFiles.AddRange(foundFiles);
                        }
                        else
                        {
                            WriteWarning($"Path not found: {resolvedPath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteWarning($"Error processing path '{path}': {ex.Message}");
                }
            }

            return regFiles.ToArray();
        }

        /// <summary>
        /// Applies filters to the operations list
        /// </summary>
        private List<RegistryOperation> ApplyFilters(List<RegistryOperation> operations)
        {
            var filtered = operations.AsEnumerable();

            // Filter by hive
            if (!string.IsNullOrEmpty(FilterHive))
            {
                filtered = filtered.Where(op => 
                    op.Hive.Equals(FilterHive, StringComparison.OrdinalIgnoreCase) ||
                    op.GetMappedHive().Equals(FilterHive, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by operation type
            if (!string.IsNullOrEmpty(FilterOperation))
            {
                if (Enum.TryParse<RegistryOperationType>(FilterOperation, true, out var operationType))
                {
                    filtered = filtered.Where(op => op.Operation == operationType);
                }
            }

            return filtered.ToList();
        }
    }
}
