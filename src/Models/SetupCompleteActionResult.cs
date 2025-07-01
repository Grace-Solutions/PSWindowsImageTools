using System;
using System.IO;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents the result of adding an action to SetupComplete.cmd
    /// </summary>
    public class SetupCompleteActionResult
    {
        /// <summary>
        /// The path to the mounted image where the action was added
        /// </summary>
        public DirectoryInfo ImagePath { get; set; } = null!;

        /// <summary>
        /// The path to the SetupComplete.cmd file
        /// </summary>
        public FileInfo SetupCompletePath { get; set; } = null!;

        /// <summary>
        /// The commands that were added to SetupComplete.cmd
        /// </summary>
        public string[] Commands { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Description of the action
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Priority order of the action
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Whether the action continues on error
        /// </summary>
        public bool ContinueOnError { get; set; }

        /// <summary>
        /// Files that were copied to the image
        /// </summary>
        public string[] CopiedFiles { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Script file that was copied to the image
        /// </summary>
        public FileInfo? ScriptFile { get; set; }

        /// <summary>
        /// Whether the action was added successfully
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Error message if the action failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// When the action was added
        /// </summary>
        public DateTime ActionTime { get; set; }

        /// <summary>
        /// Number of commands added
        /// </summary>
        public int CommandCount => Commands?.Length ?? 0;

        /// <summary>
        /// Number of files copied
        /// </summary>
        public int CopiedFileCount => CopiedFiles?.Length ?? 0;

        /// <summary>
        /// Whether the action had any errors
        /// </summary>
        public bool HasError => !IsSuccessful;

        /// <summary>
        /// Whether any files were copied
        /// </summary>
        public bool HasCopiedFiles => CopiedFileCount > 0;

        /// <summary>
        /// Whether a script file was included
        /// </summary>
        public bool HasScriptFile => ScriptFile != null;

        /// <summary>
        /// Returns a string representation of the action result
        /// </summary>
        public override string ToString()
        {
            var status = IsSuccessful ? "Success" : "Failed";
            var commandText = CommandCount == 1 ? "command" : "commands";
            return $"{Description} - {CommandCount} {commandText} added - {status}";
        }
    }
}
