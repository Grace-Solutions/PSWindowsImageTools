using System;
using System.Management.Automation;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for standardized progress reporting across the module
    /// </summary>
    public static class ProgressService
    {
        /// <summary>
        /// Creates a progress callback for file operations with counter format
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for progress reporting</param>
        /// <param name="activity">The main activity description</param>
        /// <param name="currentItem">Current item being processed</param>
        /// <param name="currentIndex">Current index (1-based)</param>
        /// <param name="totalCount">Total count of items</param>
        /// <param name="parentId">Parent progress ID for hierarchical progress</param>
        /// <returns>Progress callback action</returns>
        public static Action<int, string> CreateProgressCallback(
            PSCmdlet cmdlet,
            string activity,
            string currentItem,
            int currentIndex,
            int totalCount,
            int parentId = 0)
        {
            return (percentage, operation) =>
            {
                var statusDescription = totalCount > 1
                    ? $"{currentIndex} of {totalCount} - {currentItem}: {operation}"
                    : $"{currentItem}: {operation}";

                var progressRecord = new ProgressRecord(parentId + 1, activity, statusDescription);
                
                if (percentage >= 0 && percentage <= 100)
                {
                    progressRecord.PercentComplete = percentage;
                }

                progressRecord.ParentActivityId = parentId;
                cmdlet.WriteProgress(progressRecord);
            };
        }

        /// <summary>
        /// Creates a progress callback for file downloads with counter format
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for progress reporting</param>
        /// <param name="activity">The main activity description</param>
        /// <param name="fileName">File being downloaded</param>
        /// <param name="currentIndex">Current index (1-based)</param>
        /// <param name="totalCount">Total count of files</param>
        /// <param name="parentId">Parent progress ID for hierarchical progress</param>
        /// <returns>Progress callback action</returns>
        public static Action<int, string> CreateDownloadProgressCallback(
            PSCmdlet cmdlet,
            string activity,
            string fileName,
            int currentIndex,
            int totalCount,
            int parentId = 0)
        {
            return (percentage, operation) =>
            {
                var statusDescription = totalCount > 1 
                    ? $"{currentIndex} of {totalCount} - {fileName}"
                    : fileName;

                var progressRecord = new ProgressRecord(parentId + 1, activity, statusDescription)
                {
                    CurrentOperation = operation,
                    ParentActivityId = parentId
                };
                
                if (percentage >= 0 && percentage <= 100)
                {
                    progressRecord.PercentComplete = percentage;
                }

                cmdlet.WriteProgress(progressRecord);
            };
        }

        /// <summary>
        /// Creates a progress callback for image operations with counter format
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for progress reporting</param>
        /// <param name="activity">The main activity description</param>
        /// <param name="imageName">Image being processed</param>
        /// <param name="currentIndex">Current index (1-based)</param>
        /// <param name="totalCount">Total count of images</param>
        /// <param name="parentId">Parent progress ID for hierarchical progress</param>
        /// <returns>Progress callback action</returns>
        public static Action<int, string> CreateImageProgressCallback(
            PSCmdlet cmdlet,
            string activity,
            string imageName,
            int currentIndex,
            int totalCount,
            int parentId = 0)
        {
            return (percentage, operation) =>
            {
                var statusDescription = totalCount > 1
                    ? $"{currentIndex} of {totalCount} - {imageName}"
                    : imageName;

                var progressRecord = new ProgressRecord(parentId + 1, activity, statusDescription)
                {
                    CurrentOperation = operation,
                    ParentActivityId = parentId
                };
                
                if (percentage >= 0 && percentage <= 100)
                {
                    progressRecord.PercentComplete = percentage;
                }

                cmdlet.WriteProgress(progressRecord);
            };
        }

        /// <summary>
        /// Creates a progress callback for mount operations with timing information
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for progress reporting</param>
        /// <param name="activity">The main activity description</param>
        /// <param name="imageName">Image being mounted</param>
        /// <param name="mountPath">Mount path</param>
        /// <param name="currentIndex">Current index (1-based)</param>
        /// <param name="totalCount">Total count of images</param>
        /// <param name="parentId">Parent progress ID for hierarchical progress</param>
        /// <returns>Progress callback action</returns>
        public static Action<int, string> CreateMountProgressCallback(
            PSCmdlet cmdlet,
            string activity,
            string imageName,
            string mountPath,
            int currentIndex,
            int totalCount,
            int parentId = 0)
        {
            return (percentage, operation) =>
            {
                var statusDescription = totalCount > 1
                    ? $"{currentIndex} of {totalCount} - {imageName}"
                    : imageName;

                var progressRecord = new ProgressRecord(parentId + 1, activity, statusDescription)
                {
                    CurrentOperation = $"{operation} at {mountPath}",
                    ParentActivityId = parentId
                };
                
                if (percentage >= 0 && percentage <= 100)
                {
                    progressRecord.PercentComplete = percentage;
                }

                cmdlet.WriteProgress(progressRecord);
            };
        }

        /// <summary>
        /// Creates a verbose logging callback with counter format for operations that also need logging
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for logging</param>
        /// <param name="component">Component name for logging</param>
        /// <param name="currentIndex">Current index (1-based)</param>
        /// <param name="totalCount">Total count of items</param>
        /// <returns>Logging callback action</returns>
        public static Action<string> CreateVerboseCallback(
            PSCmdlet cmdlet,
            string component,
            int currentIndex,
            int totalCount)
        {
            return (message) =>
            {
                if (cmdlet != null)
                {
                    var prefix = totalCount > 1 ? $"{currentIndex} of {totalCount} - " : "";
                    LoggingService.WriteVerbose(cmdlet, component, $"{prefix}{message}");
                }
            };
        }

        /// <summary>
        /// Formats a counter string in X of Y format
        /// </summary>
        /// <param name="current">Current index (1-based)</param>
        /// <param name="total">Total count</param>
        /// <returns>Formatted counter string</returns>
        public static string FormatCounter(int current, int total)
        {
            return $"{current} of {total}";
        }

        /// <summary>
        /// Completes a progress operation by setting it to completed state
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for progress reporting</param>
        /// <param name="activity">The activity that was being performed</param>
        /// <param name="progressId">Progress ID to complete</param>
        public static void CompleteProgress(PSCmdlet cmdlet, string activity, int progressId = 0)
        {
            var progressRecord = new ProgressRecord(progressId, activity, "Completed")
            {
                RecordType = ProgressRecordType.Completed
            };

            cmdlet.WriteProgress(progressRecord);
        }

        /// <summary>
        /// Creates a progress callback for update installation operations
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for progress reporting</param>
        /// <param name="activity">The main activity description</param>
        /// <param name="updateName">Update being installed</param>
        /// <param name="currentIndex">Current index (1-based)</param>
        /// <param name="totalCount">Total count of updates</param>
        /// <param name="parentId">Parent progress ID for hierarchical progress</param>
        /// <returns>Progress callback action</returns>
        public static Action<int, string> CreateInstallProgressCallback(
            PSCmdlet cmdlet,
            string activity,
            string updateName,
            int currentIndex,
            int totalCount,
            int parentId = 0)
        {
            return (percentage, operation) =>
            {
                var statusDescription = totalCount > 1
                    ? $"{currentIndex} of {totalCount} - {updateName}"
                    : updateName;

                var progressRecord = new ProgressRecord(parentId + 2, activity, statusDescription)
                {
                    CurrentOperation = operation,
                    ParentActivityId = parentId
                };

                if (percentage >= 0 && percentage <= 100)
                {
                    progressRecord.PercentComplete = percentage;
                }

                cmdlet.WriteProgress(progressRecord);
            };
        }
    }
}
