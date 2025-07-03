using System;
using System.Collections.Generic;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents the result of a Media Dynamic Update operation
    /// </summary>
    public class MediaDynamicUpdateResult
    {
        /// <summary>
        /// Path to the Windows installation media that was processed
        /// </summary>
        public string MediaPath { get; set; } = string.Empty;

        /// <summary>
        /// Path to the directory containing Dynamic Update packages
        /// </summary>
        public string UpdatesPath { get; set; } = string.Empty;

        /// <summary>
        /// When the operation started
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// When the operation completed
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Total duration of the operation
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Whether the operation completed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// List of WIM files that were discovered
        /// </summary>
        public List<string> DiscoveredWimFiles { get; set; } = new List<string>();

        /// <summary>
        /// List of update files that were discovered
        /// </summary>
        public List<string> DiscoveredUpdateFiles { get; set; } = new List<string>();

        /// <summary>
        /// List of images that were successfully processed
        /// </summary>
        public List<string> ProcessedImages { get; set; } = new List<string>();

        /// <summary>
        /// List of updates that were successfully applied
        /// </summary>
        public List<string> AppliedUpdates { get; set; } = new List<string>();

        /// <summary>
        /// List of errors that occurred during processing
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Summary statistics
        /// </summary>
        public MediaDynamicUpdateSummary Summary => new MediaDynamicUpdateSummary
        {
            TotalWimFiles = DiscoveredWimFiles.Count,
            TotalUpdateFiles = DiscoveredUpdateFiles.Count,
            ProcessedImages = ProcessedImages.Count,
            AppliedUpdates = AppliedUpdates.Count,
            ErrorCount = Errors.Count,
            Duration = Duration,
            Success = Success
        };

        /// <summary>
        /// Returns a string representation of the result
        /// </summary>
        public override string ToString()
        {
            var status = Success ? "Success" : "Failed";
            var duration = Duration.TotalMinutes > 1 
                ? $"{Duration.TotalMinutes:F1} minutes" 
                : $"{Duration.TotalSeconds:F0} seconds";
            
            return $"Media Dynamic Update: {status} ({duration}) - {ProcessedImages.Count} images, {AppliedUpdates.Count} updates";
        }
    }

    /// <summary>
    /// Summary statistics for Media Dynamic Update operation
    /// </summary>
    public class MediaDynamicUpdateSummary
    {
        /// <summary>
        /// Total number of WIM files discovered
        /// </summary>
        public int TotalWimFiles { get; set; }

        /// <summary>
        /// Total number of update files discovered
        /// </summary>
        public int TotalUpdateFiles { get; set; }

        /// <summary>
        /// Number of images successfully processed
        /// </summary>
        public int ProcessedImages { get; set; }

        /// <summary>
        /// Number of updates successfully applied
        /// </summary>
        public int AppliedUpdates { get; set; }

        /// <summary>
        /// Number of errors encountered
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Total operation duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Success rate as a percentage
        /// </summary>
        public double SuccessRate => ProcessedImages > 0 ? (double)(ProcessedImages - ErrorCount) / ProcessedImages * 100 : 0;

        /// <summary>
        /// Returns a formatted summary string
        /// </summary>
        public override string ToString()
        {
            return $"WIM Files: {TotalWimFiles}, Updates: {TotalUpdateFiles}, " +
                   $"Processed: {ProcessedImages}, Applied: {AppliedUpdates}, " +
                   $"Errors: {ErrorCount}, Success Rate: {SuccessRate:F1}%";
        }
    }
}
