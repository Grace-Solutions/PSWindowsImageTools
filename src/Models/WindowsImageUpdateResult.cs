using System;
using System.IO;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents the result of installing a Windows update into a mounted image
    /// </summary>
    public class WindowsImageUpdateResult
    {
        /// <summary>
        /// The update file that was installed
        /// </summary>
        public FileInfo UpdateFile { get; set; } = null!;

        /// <summary>
        /// The path to the mounted image where the update was installed
        /// </summary>
        public DirectoryInfo ImagePath { get; set; } = null!;

        /// <summary>
        /// Whether the installation was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Error message if the installation failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// When the installation was performed
        /// </summary>
        public DateTime InstallationTime { get; set; }

        /// <summary>
        /// The name of the update file
        /// </summary>
        public string UpdateName => UpdateFile?.Name ?? string.Empty;

        /// <summary>
        /// The size of the update file in bytes
        /// </summary>
        public long UpdateSize => UpdateFile?.Length ?? 0;

        /// <summary>
        /// The size of the update file formatted for display
        /// </summary>
        public string UpdateSizeFormatted => FormatSize(UpdateSize);

        /// <summary>
        /// The type of update file (CAB or MSU)
        /// </summary>
        public string UpdateType => UpdateFile?.Extension.ToUpperInvariant().TrimStart('.') ?? string.Empty;

        /// <summary>
        /// Whether the update installation had any errors
        /// </summary>
        public bool HasError => !IsSuccessful;

        /// <summary>
        /// Formats a byte size into the most appropriate unit (B, KB, MB, GB, TB)
        /// </summary>
        private static string FormatSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int unitIndex = 0;
            double size = bytes;
            
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            
            return unitIndex == 0 
                ? $"{size:F0} {units[unitIndex]}" 
                : $"{size:F2} {units[unitIndex]}";
        }

        /// <summary>
        /// Returns a string representation of the update result
        /// </summary>
        public override string ToString()
        {
            var status = IsSuccessful ? "Success" : "Failed";
            return $"{UpdateName} ({UpdateSizeFormatted}) - {status}";
        }
    }
}
