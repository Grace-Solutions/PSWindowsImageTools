using System;
using System.IO;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for managing module configuration
    /// </summary>
    public static class ConfigurationService
    {

        /// <summary>
        /// Gets the default mount root directory
        /// </summary>
        public static string DefaultMountRootDirectory
        {
            get
            {
                var tempPath = Path.GetTempPath();
                return Path.Combine(tempPath, "PSWindowsImageTools", "Mounts");
            }
        }

        /// <summary>
        /// Resets configuration to defaults
        /// </summary>
        public static void ResetToDefaults()
        {
            // No configuration to reset currently
        }

        /// <summary>
        /// Validates the mount root directory and creates it if needed
        /// </summary>
        /// <param name="path">Path to validate</param>
        /// <returns>True if path is valid and accessible</returns>
        public static bool ValidateMountRootDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                // Test write access
                var testDir = Path.Combine(path, $"test_{Guid.NewGuid()}");
                Directory.CreateDirectory(testDir);
                Directory.Delete(testDir);

                return true;
            }
            catch
            {
                return false;
            }
        }



        /// <summary>
        /// Expands environment variables in a path
        /// </summary>
        /// <param name="path">Path that may contain environment variables</param>
        /// <returns>Expanded path</returns>
        public static string ExpandEnvironmentVariables(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            return Environment.ExpandEnvironmentVariables(path);
        }

        /// <summary>
        /// Creates a unique mount directory using GUID structure: mountroot\imageguid\imageindex
        /// </summary>
        /// <param name="mountRootDirectory">Root directory for mounts</param>
        /// <param name="imageIndex">Image index for identification</param>
        /// <param name="wimGuid">Optional GUID for the WIM file (generates new if not provided)</param>
        /// <returns>Unique mount directory path</returns>
        public static string CreateUniqueMountDirectory(string mountRootDirectory, int imageIndex, string? wimGuid = null)
        {
            var imageGuid = wimGuid ?? Guid.NewGuid().ToString();
            var mountDir = Path.Combine(mountRootDirectory, imageGuid, imageIndex.ToString());

            if (!Directory.Exists(mountDir))
            {
                Directory.CreateDirectory(mountDir);
            }

            return mountDir;
        }

        /// <summary>
        /// Cleans up mount directories in the mount root
        /// </summary>
        /// <param name="mountRootDirectory">Root directory for mounts</param>
        /// <param name="olderThanHours">Clean up directories older than this many hours (default: 24)</param>
        public static void CleanupMountDirectories(string mountRootDirectory, int olderThanHours = 24)
        {
            try
            {
                if (!Directory.Exists(mountRootDirectory))
                {
                    return;
                }

                var cutoffTime = DateTime.Now.AddHours(-olderThanHours);
                var directories = Directory.GetDirectories(mountRootDirectory);

                foreach (var directory in directories)
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(directory);
                        if (dirInfo.CreationTime < cutoffTime)
                        {
                            // Check if directory is empty or contains only empty subdirectories
                            if (IsDirectoryEmptyOrContainsOnlyEmptyDirectories(directory))
                            {
                                Directory.Delete(directory, true);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors when cleaning up individual directories
                    }
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        /// <summary>
        /// Checks if a directory is empty or contains only empty subdirectories
        /// </summary>
        /// <param name="directoryPath">Directory to check</param>
        /// <returns>True if directory is effectively empty</returns>
        private static bool IsDirectoryEmptyOrContainsOnlyEmptyDirectories(string directoryPath)
        {
            try
            {
                var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                return files.Length == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current module version in the specified format
        /// </summary>
        /// <returns>Version string in yyyy.MM.dd.HHmm format</returns>
        public static string GetCurrentVersion()
        {
            return DateTime.UtcNow.ToString("yyyy.MM.dd.HHmm");
        }

        /// <summary>
        /// Gets module information
        /// </summary>
        /// <returns>Module information object</returns>
        public static ModuleInfo GetModuleInfo()
        {
            return new ModuleInfo
            {
                Name = "PSWindowsImageTools",
                Version = GetCurrentVersion(),
                Description = "PowerShell module for Windows image customization and management",
                Author = "PSWindowsImageTools",
                Copyright = "Copyright (c) 2025 PSWindowsImageTools. All rights reserved.",

                DefaultMountRootDirectory = DefaultMountRootDirectory
            };
        }
    }

    /// <summary>
    /// Module information class
    /// </summary>
    public class ModuleInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Copyright { get; set; } = string.Empty;
        public string DefaultMountRootDirectory { get; set; } = string.Empty;
    }
}
