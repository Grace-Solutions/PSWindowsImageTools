using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Microsoft.Dism;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Adds INF drivers to mounted Windows images
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "INFDriverList")]
    [OutputType(typeof(DriverInstallationResult[]))]
    public class AddINFDriverListCmdlet : PSCmdlet
    {
        private const string ComponentName = "Add-INFDriverList";
        private readonly List<MountedWindowsImage> _allMountedImages = new List<MountedWindowsImage>();
        private readonly List<INFDriverInfo> _allDrivers = new List<INFDriverInfo>();

        /// <summary>
        /// Mounted Windows images to add drivers to
        /// </summary>
        [Parameter(
            Mandatory = true,
            Position = 0,
            ValueFromPipeline = true,
            ParameterSetName = "ByMountedImage",
            HelpMessage = "Mounted Windows images to add drivers to")]
        [ValidateNotNull]
        public MountedWindowsImage[] MountedImages { get; set; } = Array.Empty<MountedWindowsImage>();

        /// <summary>
        /// INF driver objects to install (from Get-INFDriverList)
        /// </summary>
        [Parameter(
            Mandatory = true,
            Position = 1,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "INF driver objects to install (from Get-INFDriverList)")]
        [ValidateNotNull]
        public INFDriverInfo[] Drivers { get; set; } = Array.Empty<INFDriverInfo>();

        /// <summary>
        /// Force installation of unsigned drivers
        /// </summary>
        [Parameter(
            Mandatory = false,
            HelpMessage = "Force installation of unsigned drivers")]
        public SwitchParameter ForceUnsigned { get; set; }

        /// <summary>
        /// Processes pipeline input
        /// </summary>
        protected override void ProcessRecord()
        {
            _allMountedImages.AddRange(MountedImages);
            _allDrivers.AddRange(Drivers);
        }

        /// <summary>
        /// Performs the driver installation operation
        /// </summary>
        protected override void EndProcessing()
        {
            if (_allMountedImages.Count == 0)
            {
                LoggingService.WriteWarning(this, "No mounted images provided for driver installation");
                return;
            }

            if (_allDrivers.Count == 0)
            {
                LoggingService.WriteWarning(this, "No drivers provided for installation");
                return;
            }

            var operationStartTime = LoggingService.LogOperationStartWithTimestamp(this, ComponentName, "Install INF Drivers",
                $"Installing {_allDrivers.Count} drivers on {_allMountedImages.Count} mounted images");

            var results = new List<DriverInstallationResult>();

            try
            {
                // Process each mounted image
                for (int i = 0; i < _allMountedImages.Count; i++)
                {
                    var mountedImage = _allMountedImages[i];
                    var imageProgress = (int)((double)(i + 1) / _allMountedImages.Count * 100);

                    LoggingService.WriteProgress(this, "Installing INF Drivers",
                        $"[{i + 1} of {_allMountedImages.Count}] - {mountedImage.ImageName}",
                        $"Processing {mountedImage.MountPath.FullName} ({imageProgress}%)", imageProgress);

                    try
                    {
                        var result = InstallDriversOnImage(mountedImage, _allDrivers, i + 1, _allMountedImages.Count);
                        results.Add(result);

                        LoggingService.WriteVerbose(this, 
                            $"[{i + 1} of {_allMountedImages.Count}] - Completed driver installation on {mountedImage.ImageName}: " +
                            $"{result.SuccessCount} successful, {result.FailureCount} failed");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.WriteError(this, ComponentName,
                            $"[{i + 1} of {_allMountedImages.Count}] - Failed to install drivers on {mountedImage.ImageName}: {ex.Message}", ex);

                        // Create a failed result
                        var failedResult = new DriverInstallationResult
                        {
                            MountedImage = mountedImage,
                            FailedDrivers = _allDrivers.ToList()
                        };
                        failedResult.ErrorMessages["General"] = ex.Message;
                        results.Add(failedResult);
                    }
                }

                // Output results
                foreach (var result in results)
                {
                    WriteObject(result);
                }

                // Summary
                var totalSuccessful = results.Sum(r => r.SuccessCount);
                var totalFailed = results.Sum(r => r.FailureCount);
                var totalProcessed = totalSuccessful + totalFailed;

                var successPercentage = totalProcessed > 0 ? (double)totalSuccessful / totalProcessed * 100 : 0;

                LoggingService.LogOperationCompleteWithTimestamp(this, ComponentName, "Install INF Drivers", operationStartTime,
                    $"Completed: {totalSuccessful} successful, {totalFailed} failed installations ({successPercentage:F1}%)");
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName, $"Failed to install INF drivers: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Installs drivers on a single mounted image
        /// </summary>
        private DriverInstallationResult InstallDriversOnImage(
            MountedWindowsImage mountedImage,
            List<INFDriverInfo> drivers,
            int currentImageIndex,
            int totalImages)
        {
            var result = new DriverInstallationResult
            {
                MountedImage = mountedImage
            };

            LoggingService.WriteVerbose(this,
                $"[{currentImageIndex} of {totalImages}] - Installing {drivers.Count} drivers on {mountedImage.ImageName}");

            try
            {
                using (var session = DismApi.OpenOfflineSession(mountedImage.MountPath.FullName))
                {
                    for (int i = 0; i < drivers.Count; i++)
                    {
                        var driver = drivers[i];
                        var driverProgress = (int)((double)(i + 1) / drivers.Count * 100);

                        LoggingService.WriteProgress(this, "Installing INF Drivers",
                            $"[{currentImageIndex} of {totalImages}] - {mountedImage.ImageName}",
                            $"Installing driver {i + 1} of {drivers.Count}: {driver.INFFile.Name} ({driverProgress}%)",
                            driverProgress);

                        try
                        {
                            InstallSingleDriver(session, driver, result);
                            LoggingService.WriteVerbose(this,
                                $"[{currentImageIndex} of {totalImages}] - Successfully installed driver: {driver.INFFile.Name}");
                        }
                        catch (Exception ex)
                        {
                            result.FailedDrivers.Add(driver);
                            result.ErrorMessages[driver.INFFile.Name] = ex.Message;
                            LoggingService.WriteWarning(this,
                                $"[{currentImageIndex} of {totalImages}] - Failed to install driver {driver.INFFile.Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteError(this, ComponentName,
                    $"[{currentImageIndex} of {totalImages}] - Failed to open DISM session for {mountedImage.MountPath.FullName}: {ex.Message}", ex);
                
                // Mark all drivers as failed
                result.FailedDrivers.AddRange(drivers);
                result.ErrorMessages["Session"] = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Installs a single driver using DISM API
        /// </summary>
        private void InstallSingleDriver(DismSession session, INFDriverInfo driver, DriverInstallationResult result)
        {
            try
            {
                LoggingService.WriteVerbose(this, $"Installing driver: {driver.INFFile.FullName}");

                // Add the driver using DISM API
                DismApi.AddDriver(session, driver.INFFile.FullName, ForceUnsigned.IsPresent);

                result.SuccessfulDrivers.Add(driver);
                LoggingService.WriteVerbose(this, $"Successfully installed driver: {driver.INFFile.Name}");
            }
            catch (Exception ex)
            {
                LoggingService.WriteWarning(this, $"Failed to install driver {driver.INFFile.Name}: {ex.Message}");
                throw;
            }
        }
    }
}
