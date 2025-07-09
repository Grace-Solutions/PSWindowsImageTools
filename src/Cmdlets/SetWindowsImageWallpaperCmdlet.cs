using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Text.RegularExpressions;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Sets wallpaper and lockscreen images for mounted Windows images
    /// Follows the proven approach from Invoke-WallpaperConfiguration.ps1
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "WindowsImageWallpaper", SupportsShouldProcess = true)]
    [OutputType(typeof(WallpaperConfigurationService.ConfigurationResult))]
    public class SetWindowsImageWallpaperCmdlet : PSCmdlet
    {
        #region Parameters

        /// <summary>
        /// Mount path for direct path-based configuration
        /// </summary>
        [Parameter(
            ParameterSetName = "ByPath",
            Mandatory = true,
            Position = 0,
            HelpMessage = "Path where the Windows image is mounted")]
        [ValidateNotNull]
        public DirectoryInfo MountPath { get; set; } = null!;

        /// <summary>
        /// Mounted Windows image objects from pipeline
        /// </summary>
        [Parameter(
            ParameterSetName = "ByObject",
            ValueFromPipeline = true,
            Mandatory = true,
            HelpMessage = "Mounted Windows image objects from Get-WindowsImageList -Advanced")]
        [ValidateNotNull]
        public MountedWindowsImage[] MountedImages { get; set; } = null!;

        /// <summary>
        /// Source wallpaper image file
        /// </summary>
        [Parameter(
            Mandatory = true,
            HelpMessage = "Path to the wallpaper image file (jpg, jpeg, png, bmp)")]
        [ValidateNotNull]
        public FileInfo WallpaperPath { get; set; } = null!;

        /// <summary>
        /// Source lockscreen image file (optional)
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Path to the lockscreen image file (jpg, jpeg, png, bmp)")]
        public FileInfo? LockscreenPath { get; set; }

        /// <summary>
        /// Custom resolution list (uses default if not specified)
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Custom resolution list for wallpaper generation")]
        public ResolutionInfo[] ResolutionList { get; set; } = ResolutionInfo.GetDefaultResolutions();

        /// <summary>
        /// Force overwrite existing files
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Force overwrite existing wallpaper files")]
        public SwitchParameter Force { get; set; }

        #endregion

        #region Private Fields

        private readonly List<DirectoryInfo> _directoriesToProcess = new List<DirectoryInfo>();
        private static readonly Regex ImageExtensionPattern = new Regex(@"\.(jpg|jpeg|png|bmp)$", RegexOptions.IgnoreCase);

        #endregion

        #region Cmdlet Overrides

        /// <summary>
        /// Validates parameters before processing
        /// </summary>
        protected override void BeginProcessing()
        {
            try
            {
                LoggingService.WriteVerbose(this, "Starting wallpaper configuration");

                // Validate image file extensions
                if (!ImageExtensionPattern.IsMatch(WallpaperPath.Extension))
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new ArgumentException($"Wallpaper file must have a valid image extension (jpg, jpeg, png, bmp): {WallpaperPath.FullName}"),
                        "InvalidWallpaperExtension",
                        ErrorCategory.InvalidArgument,
                        WallpaperPath));
                    return;
                }

                if (LockscreenPath != null && !ImageExtensionPattern.IsMatch(LockscreenPath.Extension))
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new ArgumentException($"Lockscreen file must have a valid image extension (jpg, jpeg, png, bmp): {LockscreenPath.FullName}"),
                        "InvalidLockscreenExtension",
                        ErrorCategory.InvalidArgument,
                        LockscreenPath));
                    return;
                }

                // Validate source files exist
                if (!WallpaperPath.Exists)
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new FileNotFoundException($"Wallpaper file not found: {WallpaperPath.FullName}"),
                        "WallpaperFileNotFound",
                        ErrorCategory.ObjectNotFound,
                        WallpaperPath));
                    return;
                }

                if (LockscreenPath != null && !LockscreenPath.Exists)
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new FileNotFoundException($"Lockscreen file not found: {LockscreenPath.FullName}"),
                        "LockscreenFileNotFound",
                        ErrorCategory.ObjectNotFound,
                        LockscreenPath));
                    return;
                }

                LoggingService.WriteVerbose(this, $"Wallpaper source: {WallpaperPath.FullName}");
                if (LockscreenPath != null)
                {
                    LoggingService.WriteVerbose(this, $"Lockscreen source: {LockscreenPath.FullName}");
                }
                LoggingService.WriteVerbose(this, $"Resolution list contains {ResolutionList.Length} resolutions");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, "SetWindowsImageWallpaper", $"Parameter validation failed: {ex.Message}", ex);
                ThrowTerminatingError(new ErrorRecord(
                    ex,
                    "ParameterValidationFailed",
                    ErrorCategory.InvalidArgument,
                    null));
            }
        }

        /// <summary>
        /// Processes pipeline input
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                if (ParameterSetName == "ByObject")
                {
                    foreach (var mountedImage in MountedImages)
                    {
                        if (mountedImage.MountPath != null && mountedImage.MountPath.Exists)
                        {
                            _directoriesToProcess.Add(mountedImage.MountPath);
                        }
                        else
                        {
                            WriteWarning($"Mount path does not exist for image: {mountedImage.ImageName}");
                        }
                    }
                    LoggingService.WriteVerbose(this, $"Added {MountedImages.Length} mounted image directories to processing queue");
                }
                else if (ParameterSetName == "ByPath")
                {
                    if (MountPath.Exists)
                    {
                        _directoriesToProcess.Add(MountPath);
                        LoggingService.WriteVerbose(this, $"Added direct mount path to processing queue: {MountPath.FullName}");
                    }
                    else
                    {
                        ThrowTerminatingError(new ErrorRecord(
                            new DirectoryNotFoundException($"Mount path does not exist: {MountPath.FullName}"),
                            "MountPathNotFound",
                            ErrorCategory.ObjectNotFound,
                            MountPath));
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, "SetWindowsImageWallpaper", $"Failed to process input: {ex.Message}", ex);
                WriteError(new ErrorRecord(
                    ex,
                    "ProcessInputFailed",
                    ErrorCategory.InvalidOperation,
                    null));
            }
        }

        /// <summary>
        /// Processes all collected directories
        /// </summary>
        protected override void EndProcessing()
        {
            try
            {
                if (_directoriesToProcess.Count == 0)
                {
                    WriteWarning("No directories to process");
                    return;
                }

                LoggingService.WriteVerbose(this, $"Processing wallpaper configuration for {_directoriesToProcess.Count} directory(ies)");

                using var wallpaperService = new WallpaperConfigurationService();

                for (int i = 0; i < _directoriesToProcess.Count; i++)
                {
                    var directory = _directoriesToProcess[i];

                    try
                    {
                        LoggingService.WriteVerbose(this, $"[{i + 1} of {_directoriesToProcess.Count}] Processing directory: {directory.FullName}");

                        // Create wallpaper configuration
                        var configuration = new WallpaperConfiguration(
                            directory,
                            WallpaperPath,
                            LockscreenPath,
                            ResolutionList);

                        // Validate configuration
                        configuration.Validate();

                        // Process wallpaper configuration if ShouldProcess confirms
                        if (ShouldProcess(directory.FullName, "Configure wallpaper and lockscreen"))
                        {
                            var result = wallpaperService.ConfigureWallpaper(configuration, this);

                            if (result.Success)
                            {
                                LoggingService.WriteVerbose(this, $"Successfully configured wallpaper for: {directory.FullName}");
                                LoggingService.WriteVerbose(this, $"Processed {result.ProcessedFiles.Count} files");

                                if (result.Warnings.Count > 0)
                                {
                                    foreach (var warning in result.Warnings)
                                    {
                                        WriteWarning(warning);
                                    }
                                }
                            }
                            else
                            {
                                WriteError(new ErrorRecord(
                                    new InvalidOperationException(result.ErrorMessage ?? "Unknown error"),
                                    "WallpaperConfigurationFailed",
                                    ErrorCategory.InvalidOperation,
                                    directory));
                            }

                            // Output result object
                            WriteObject(result);
                        }
                        else
                        {
                            LoggingService.WriteVerbose(this, $"Skipped wallpaper configuration for: {directory.FullName} (WhatIf or user declined)");
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteError(this, "SetWindowsImageWallpaper", $"Failed to configure wallpaper for {directory.FullName}: {ex.Message}", ex);
                        WriteError(new ErrorRecord(
                            ex,
                            "WallpaperConfigurationError",
                            ErrorCategory.InvalidOperation,
                            directory));
                    }
                }

                LoggingService.WriteVerbose(this, "Wallpaper configuration completed for all directories");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, "SetWindowsImageWallpaper", $"Failed to complete wallpaper configuration: {ex.Message}", ex);
                ThrowTerminatingError(new ErrorRecord(
                    ex,
                    "WallpaperConfigurationFailed",
                    ErrorCategory.InvalidOperation,
                    null));
            }
        }

        #endregion
    }
}
