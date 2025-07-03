using System;
using System.Collections.Generic;
using System.IO;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents information about an installed Windows Assessment and Deployment Kit (ADK)
    /// </summary>
    public class ADKInfo
    {
        /// <summary>
        /// Version of the ADK installation
        /// </summary>
        public Version Version { get; set; } = new Version();

        /// <summary>
        /// Root installation directory of the ADK
        /// </summary>
        public DirectoryInfo InstallationPath { get; set; } = null!;

        /// <summary>
        /// Path to the WinPE Optional Components directory
        /// </summary>
        public DirectoryInfo? WinPEOptionalComponentsPath { get; set; }

        /// <summary>
        /// Path to the DISM executable
        /// </summary>
        public FileInfo? DismPath { get; set; }

        /// <summary>
        /// Path to the ImageX executable (legacy)
        /// </summary>
        public FileInfo? ImageXPath { get; set; }

        /// <summary>
        /// Whether WinPE add-on is installed
        /// </summary>
        public bool HasWinPEAddon { get; set; }

        /// <summary>
        /// Whether Deployment Tools are installed
        /// </summary>
        public bool HasDeploymentTools { get; set; }

        /// <summary>
        /// Whether Windows System Image Manager is installed
        /// </summary>
        public bool HasWindowsSystemImageManager { get; set; }

        /// <summary>
        /// Whether User State Migration Tool is installed
        /// </summary>
        public bool HasUSMT { get; set; }

        /// <summary>
        /// List of available architectures (x86, amd64, arm64)
        /// </summary>
        public List<string> SupportedArchitectures { get; set; } = new List<string>();

        /// <summary>
        /// Registry key where ADK information was found
        /// </summary>
        public string RegistryKey { get; set; } = string.Empty;

        /// <summary>
        /// Display name from registry
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Publisher information
        /// </summary>
        public string Publisher { get; set; } = string.Empty;

        /// <summary>
        /// Installation date
        /// </summary>
        public DateTime? InstallDate { get; set; }

        /// <summary>
        /// Returns a string representation of the ADK installation
        /// </summary>
        public override string ToString()
        {
            return $"Windows ADK {Version} at {InstallationPath?.FullName ?? "Unknown"}";
        }
    }

    /// <summary>
    /// Represents a WinPE Optional Component
    /// </summary>
    public class WinPEOptionalComponent
    {
        /// <summary>
        /// Name of the optional component
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the component
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Description of the component
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Category of the component (e.g., "Networking", "Storage", "Scripting")
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Architecture this component supports
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// CAB file for the component
        /// </summary>
        public FileInfo ComponentFile { get; set; } = null!;

        /// <summary>
        /// Language pack CAB files for this component
        /// </summary>
        public List<FileInfo> LanguagePackFiles { get; set; } = new List<FileInfo>();

        /// <summary>
        /// Dependencies required by this component
        /// </summary>
        public List<string> Dependencies { get; set; } = new List<string>();

        /// <summary>
        /// Size of the component in bytes
        /// </summary>
        public long SizeInBytes { get; set; }

        /// <summary>
        /// Whether this component is a language pack
        /// </summary>
        public bool IsLanguagePack { get; set; }

        /// <summary>
        /// Language code if this is a language pack
        /// </summary>
        public string? LanguageCode { get; set; }

        /// <summary>
        /// Version of the component
        /// </summary>
        public Version? Version { get; set; }

        /// <summary>
        /// Whether the component file exists and is accessible
        /// </summary>
        public bool IsAvailable => ComponentFile?.Exists == true;

        /// <summary>
        /// Formatted size string
        /// </summary>
        public string SizeFormatted
        {
            get
            {
                if (SizeInBytes == 0) return "Unknown";
                
                const long KB = 1024;
                const long MB = KB * 1024;
                const long GB = MB * 1024;

                if (SizeInBytes >= GB)
                    return $"{SizeInBytes / (double)GB:F2} GB";
                if (SizeInBytes >= MB)
                    return $"{SizeInBytes / (double)MB:F2} MB";
                if (SizeInBytes >= KB)
                    return $"{SizeInBytes / (double)KB:F2} KB";
                
                return $"{SizeInBytes} bytes";
            }
        }

        /// <summary>
        /// Returns a string representation of the optional component
        /// </summary>
        public override string ToString()
        {
            return $"{DisplayName} ({Architecture}) - {SizeFormatted}";
        }
    }

    /// <summary>
    /// Represents the result of installing optional components into a boot image
    /// </summary>
    public class OptionalComponentInstallationResult
    {
        /// <summary>
        /// The mounted image that components were installed to
        /// </summary>
        public MountedWindowsImage MountedImage { get; set; } = new MountedWindowsImage();

        /// <summary>
        /// List of components that were successfully installed
        /// </summary>
        public List<WinPEOptionalComponent> SuccessfulComponents { get; set; } = new List<WinPEOptionalComponent>();

        /// <summary>
        /// List of components that failed to install
        /// </summary>
        public List<WinPEOptionalComponent> FailedComponents { get; set; } = new List<WinPEOptionalComponent>();

        /// <summary>
        /// List of components that were skipped (already installed)
        /// </summary>
        public List<WinPEOptionalComponent> SkippedComponents { get; set; } = new List<WinPEOptionalComponent>();

        /// <summary>
        /// Total number of components processed
        /// </summary>
        public int TotalComponents => SuccessfulComponents.Count + FailedComponents.Count + SkippedComponents.Count;

        /// <summary>
        /// Success rate as a percentage
        /// </summary>
        public double SuccessRate => TotalComponents > 0 ? (double)SuccessfulComponents.Count / TotalComponents * 100 : 0;

        /// <summary>
        /// Whether all components were installed successfully
        /// </summary>
        public bool AllSuccessful => FailedComponents.Count == 0 && SuccessfulComponents.Count > 0;

        /// <summary>
        /// Duration of the installation process
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Any errors encountered during installation
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Returns a string representation of the installation result
        /// </summary>
        public override string ToString()
        {
            return $"Installed {SuccessfulComponents.Count}/{TotalComponents} components ({SuccessRate:F1}% success rate)";
        }
    }
}
