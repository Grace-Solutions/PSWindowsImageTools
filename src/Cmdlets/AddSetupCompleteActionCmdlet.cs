using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Cmdlet for adding custom actions to SetupComplete.cmd during Windows imaging
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "SetupCompleteAction")]
    [OutputType(typeof(SetupCompleteActionResult))]
    public class AddSetupCompleteActionCmdlet : PSCmdlet
    {
        /// <summary>
        /// Path to the mounted Windows image directory
        /// </summary>
        [Parameter(
            Position = 0,
            Mandatory = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Path to the mounted Windows image directory")]
        [ValidateNotNullOrEmpty]
        public DirectoryInfo ImagePath { get; set; } = null!;

        /// <summary>
        /// Command or script to execute during SetupComplete phase
        /// </summary>
        [Parameter(
            Position = 1,
            ValueFromPipeline = true,
            HelpMessage = "Command or script to execute during SetupComplete phase")]
        public string[] Command { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Description of the action for documentation purposes
        /// </summary>
        [Parameter(
            HelpMessage = "Description of the action for documentation purposes")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Priority order for the action (lower numbers execute first)
        /// </summary>
        [Parameter(
            HelpMessage = "Priority order for the action (lower numbers execute first)")]
        [ValidateRange(1, 999)]
        public int Priority { get; set; } = 100;

        /// <summary>
        /// Whether to continue execution if this action fails
        /// </summary>
        [Parameter(
            HelpMessage = "Whether to continue execution if this action fails")]
        public SwitchParameter ContinueOnError { get; set; }

        /// <summary>
        /// Path to a script file to copy and execute
        /// </summary>
        [Parameter(
            HelpMessage = "Path to a script file to copy and execute")]
        public FileInfo ScriptFile { get; set; } = null!;

        /// <summary>
        /// Path to files/directories to copy to the image
        /// </summary>
        [Parameter(
            HelpMessage = "Path to files/directories to copy to the image")]
        public FileSystemInfo[] CopyFiles { get; set; } = Array.Empty<FileSystemInfo>();

        /// <summary>
        /// Destination path in the image for copied files (relative to C:\)
        /// </summary>
        [Parameter(
            HelpMessage = "Destination path in the image for copied files (relative to C:)")]
        public string CopyDestination { get; set; } = "Temp\\SetupComplete";

        /// <summary>
        /// Whether to create a backup of the existing SetupComplete.cmd
        /// </summary>
        [Parameter(
            HelpMessage = "Whether to create a backup of the existing SetupComplete.cmd")]
        public SwitchParameter Backup { get; set; }

        private const string ComponentName = "SetupCompleteAction";

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName, "Add SetupComplete Action");

                // Validate image path
                ValidateImagePath();

                // Validate that at least one action is specified
                if ((Command == null || Command.Length == 0) && ScriptFile == null)
                {
                    throw new ArgumentException("At least one of Command or ScriptFile must be specified");
                }

                // Get SetupComplete.cmd path
                var setupCompletePath = GetSetupCompletePath();
                LoggingService.WriteVerbose(this, $"SetupComplete.cmd path: {setupCompletePath}");

                // Create backup if requested
                if (Backup.IsPresent)
                {
                    CreateBackup(setupCompletePath);
                }

                // Copy files if specified
                var copiedFiles = new List<string>();
                if (CopyFiles.Length > 0)
                {
                    copiedFiles = CopyFilesToImage();
                }

                // Copy script file if specified
                string scriptPath = string.Empty;
                if (ScriptFile != null)
                {
                    scriptPath = CopyScriptToImage();
                }

                // Generate commands
                var commands = GenerateCommands(scriptPath, copiedFiles);

                // Add commands to SetupComplete.cmd
                AddCommandsToSetupComplete(setupCompletePath, commands);

                // Create result
                var result = new SetupCompleteActionResult
                {
                    ImagePath = ImagePath,
                    SetupCompletePath = new FileInfo(setupCompletePath),
                    Commands = commands.ToArray(),
                    Description = Description,
                    Priority = Priority,
                    ContinueOnError = ContinueOnError.IsPresent,
                    CopiedFiles = copiedFiles.ToArray(),
                    ScriptFile = !string.IsNullOrEmpty(scriptPath) ? new FileInfo(scriptPath) : null,
                    IsSuccessful = true,
                    ActionTime = DateTime.UtcNow
                };

                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Add SetupComplete Action", operationStartTime,
                    $"Added {commands.Count} command(s) to SetupComplete.cmd");

                WriteObject(result);
            }
            catch (Exception ex)
            {
                LoggingService.LogOperationFailure(this, ComponentName, ex);
                
                var result = new SetupCompleteActionResult
                {
                    ImagePath = ImagePath,
                    IsSuccessful = false,
                    ErrorMessage = ex.Message,
                    ActionTime = DateTime.UtcNow
                };

                WriteObject(result);
            }
        }

        /// <summary>
        /// Validates that the image path is a valid mounted Windows image
        /// </summary>
        private void ValidateImagePath()
        {
            if (!ImagePath.Exists)
            {
                throw new DirectoryNotFoundException($"Image path does not exist: {ImagePath.FullName}");
            }

            // Check for Windows directory
            var windowsDir = new DirectoryInfo(Path.Combine(ImagePath.FullName, "Windows"));
            if (!windowsDir.Exists)
            {
                throw new InvalidOperationException($"Windows directory not found in mounted image: {windowsDir.FullName}");
            }

            LoggingService.WriteVerbose(this, "Image path validation completed");
        }

        /// <summary>
        /// Gets the path to SetupComplete.cmd in the mounted image
        /// </summary>
        private string GetSetupCompletePath()
        {
            var setupDir = Path.Combine(ImagePath.FullName, "Windows", "Setup", "Scripts");
            var setupCompletePath = Path.Combine(setupDir, "SetupComplete.cmd");

            // Create Scripts directory if it doesn't exist
            if (!Directory.Exists(setupDir))
            {
                Directory.CreateDirectory(setupDir);
                LoggingService.WriteVerbose(this, $"Created Scripts directory: {setupDir}");
            }

            return setupCompletePath;
        }

        /// <summary>
        /// Creates a backup of the existing SetupComplete.cmd
        /// </summary>
        private void CreateBackup(string setupCompletePath)
        {
            if (File.Exists(setupCompletePath))
            {
                var backupPath = $"{setupCompletePath}.backup.{DateTime.Now:yyyyMMdd-HHmmss}";
                File.Copy(setupCompletePath, backupPath);
                LoggingService.WriteVerbose(this, $"Created backup: {backupPath}");
            }
        }

        /// <summary>
        /// Copies files to the image
        /// </summary>
        private List<string> CopyFilesToImage()
        {
            var copiedFiles = new List<string>();
            var destinationDir = Path.Combine(ImagePath.FullName, CopyDestination.TrimStart('\\', '/'));

            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
                LoggingService.WriteVerbose(this, $"Created destination directory: {destinationDir}");
            }

            foreach (var item in CopyFiles)
            {
                if (item is FileInfo file)
                {
                    var destPath = Path.Combine(destinationDir, file.Name);
                    file.CopyTo(destPath, true);
                    copiedFiles.Add(destPath);
                    LoggingService.WriteVerbose(this, $"Copied file: {file.FullName} -> {destPath}");
                }
                else if (item is DirectoryInfo directory)
                {
                    var destPath = Path.Combine(destinationDir, directory.Name);
                    CopyDirectory(directory.FullName, destPath);
                    copiedFiles.Add(destPath);
                    LoggingService.WriteVerbose(this, $"Copied directory: {directory.FullName} -> {destPath}");
                }
            }

            return copiedFiles;
        }

        /// <summary>
        /// Copies a script file to the image
        /// </summary>
        private string CopyScriptToImage()
        {
            var destinationDir = Path.Combine(ImagePath.FullName, CopyDestination.TrimStart('\\', '/'));
            
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            var destPath = Path.Combine(destinationDir, ScriptFile.Name);
            ScriptFile.CopyTo(destPath, true);
            LoggingService.WriteVerbose(this, $"Copied script: {ScriptFile.FullName} -> {destPath}");

            return destPath;
        }

        /// <summary>
        /// Generates the commands to add to SetupComplete.cmd
        /// </summary>
        private List<string> GenerateCommands(string scriptPath, List<string> copiedFiles)
        {
            var commands = new List<string>();

            // Add header comment
            commands.Add($"REM === SetupComplete Action - {Description} (Priority: {Priority}) ===");
            commands.Add($"REM Added on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            // Add script execution if specified
            if (!string.IsNullOrEmpty(scriptPath))
            {
                // Convert absolute path to relative path from C:\
                var relativePath = scriptPath.Replace(ImagePath.FullName, "").TrimStart('\\', '/').Replace('\\', '/');
                var command = $"call \"C:\\{relativePath}\"";

                if (ContinueOnError.IsPresent)
                {
                    commands.Add($"{command} || echo Warning: Script failed but continuing...");
                }
                else
                {
                    commands.Add(command);
                }
            }

            // Add custom commands
            foreach (var cmd in Command)
            {
                if (ContinueOnError.IsPresent)
                {
                    commands.Add($"{cmd} || echo Warning: Command failed but continuing...");
                }
                else
                {
                    commands.Add(cmd);
                }
            }

            commands.Add("REM === End SetupComplete Action ===");
            commands.Add("");

            return commands;
        }

        /// <summary>
        /// Adds commands to SetupComplete.cmd
        /// </summary>
        private void AddCommandsToSetupComplete(string setupCompletePath, List<string> commands)
        {
            var existingContent = string.Empty;
            if (File.Exists(setupCompletePath))
            {
                existingContent = File.ReadAllText(setupCompletePath, Encoding.UTF8);
            }

            var newContent = new StringBuilder();
            
            // Add existing content
            if (!string.IsNullOrEmpty(existingContent))
            {
                newContent.AppendLine(existingContent.TrimEnd());
                newContent.AppendLine();
            }

            // Add new commands
            foreach (var command in commands)
            {
                newContent.AppendLine(command);
            }

            File.WriteAllText(setupCompletePath, newContent.ToString(), Encoding.UTF8);
            LoggingService.WriteVerbose(this, $"Added {commands.Count} commands to SetupComplete.cmd");
        }

        /// <summary>
        /// Recursively copies a directory
        /// </summary>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }
    }
}
